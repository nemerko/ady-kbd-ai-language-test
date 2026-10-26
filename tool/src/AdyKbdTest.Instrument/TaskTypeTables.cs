using System.Globalization;
using System.Text;

namespace AdyKbdTest.Instrument;

/// <summary>One question group of the main table (revision 16 §2.1).</summary>
public sealed record QuestionGroup(string Code, string QuestionsLabel, IReadOnlyList<int> Questions);

/// <summary>
/// The question groups, in the one order every table uses (revision 16 §2.1): parallel to the
/// question order, with the combined result last. This is the only place the grouping is defined.
/// </summary>
public static class QuestionGroups
{
    public static readonly QuestionGroup Q1To3 = new("q1_3", "S1-S3", [1, 2, 3]);
    public static readonly QuestionGroup Q4 = new("q4", "S4", [4]);
    public static readonly QuestionGroup Q5 = new("q5", "S5", [5]);
    public static readonly QuestionGroup Q6To7 = new("q6_7", "S6-S7", [6, 7]);
    public static readonly QuestionGroup All = new("all", "S1-S7", [1, 2, 3, 4, 5, 6, 7]);

    /// <summary>The four task-type groups, in order (the combined group is not one of them).</summary>
    public static readonly IReadOnlyList<QuestionGroup> TaskTypes = [Q1To3, Q4, Q5, Q6To7];

    /// <summary>The four task-type groups followed by the combined one -- the order of every row set.</summary>
    public static readonly IReadOnlyList<QuestionGroup> Ordered = [Q1To3, Q4, Q5, Q6To7, All];

    public static QuestionGroup TaskTypeOf(int question) =>
        TaskTypes.Single(g => g.Questions.Contains(question));
}

/// <summary>
/// One (tool, group) row of the task-type table: raw counts only, plus the ratios derived from them
/// (revision 16 §2.2). A ratio whose denominator is zero is <c>null</c> -- written as an empty
/// cell, never as 0.
/// </summary>
public sealed record TaskTypeRow(int Tool, QuestionGroup Group, int ErrorWords, int Words, int Sentences, int Correct, int ExcludedResponses)
{
    public double? ErrorsPer100Words => Words == 0 ? null : ErrorWords * 100.0 / Words;
    public double? CorrectSentencePct => Sentences == 0 ? null : Correct * 100.0 / Sentences;
    public double? MeanSentenceLength => Sentences == 0 ? null : (double)Words / Sentences;
}

/// <summary>
/// Computes and writes the task-type tables. Both the per-test report (<c>akt report</c>) and the
/// cross-test summary (<c>akt summary</c>) write through the same two writers, so their columns can
/// not drift apart.
/// </summary>
public static class TaskTypeTables
{
    public const string LongHeader = "tool\tgroup\tquestions\terror_words\twords\terrors_per_100_words\tsentences\tcorrect\tcorrect_sentence_pct\tmean_sentence_length\texcluded_responses";

    public static string Format(double? value) =>
        value is { } v ? v.ToString("0.####", CultureInfo.InvariantCulture) : "";

    /// <summary>
    /// Rows for every (tool, group), tools in registry order and groups in <see cref="QuestionGroups.Ordered"/>
    /// order. Only evaluated sentences count (revisions 13 and 15): a response with no evaluable
    /// content and a sentence without a verdict contribute nothing but the excluded-response count.
    /// </summary>
    public static IReadOnlyList<TaskTypeRow> Compute(IReadOnlyList<ResponseMarking> markings)
    {
        var rows = new List<TaskTypeRow>();
        foreach (var tool in ToolRegistry.Tools)
        {
            foreach (var group in QuestionGroups.Ordered)
            {
                var responses = markings.Where(m => m.Id.Tool == tool.Number && group.Questions.Contains(m.Id.Question)).ToList();
                var sentences = responses.SelectMany(ReportBuilder.EvaluatedSentences).ToList();
                rows.Add(new TaskTypeRow(
                    tool.Number,
                    group,
                    sentences.Sum(s => (s.WrongForm ?? 0) + (s.WrongWord ?? 0) + (s.Invented ?? 0) + (s.CodeMixing ?? 0)),
                    responses.Sum(ReportBuilder.EvaluatedWordCount),
                    sentences.Count,
                    sentences.Count(s => s.Verdict == SentenceVerdict.Dogru),
                    responses.Count(ReportBuilder.HasNoEvaluableContent)));
            }
        }
        return rows;
    }

    /// <summary>Long form, one row per (tool, group); <paramref name="tests"/> adds a leading <c>tests</c> column (the summary).</summary>
    public static void WriteLong(string path, IReadOnlyList<TaskTypeRow> rows, string? tests = null)
    {
        var sb = new StringBuilder();
        sb.Append(tests is null ? "" : "tests\t").Append(LongHeader).Append('\n');
        foreach (var r in rows)
        {
            if (tests is not null) sb.Append(tests).Append('\t');
            sb.Append(string.Join('\t',
                r.Tool, r.Group.Code, r.Group.QuestionsLabel, r.ErrorWords, r.Words, Format(r.ErrorsPer100Words),
                r.Sentences, r.Correct, Format(r.CorrectSentencePct), Format(r.MeanSentenceLength), r.ExcludedResponses)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Wide form, one row per tool: four columns per group (the report's main table).</summary>
    public static void WriteWide(string path, IReadOnlyList<TaskTypeRow> rows)
    {
        var sb = new StringBuilder("tool");
        foreach (var g in QuestionGroups.Ordered)
        {
            sb.Append($"\t{g.Code}_errors_per_100_words\t{g.Code}_correct_sentence_pct\t{g.Code}_words\t{g.Code}_mean_sentence_length");
        }
        sb.Append('\n');

        foreach (var tool in ToolRegistry.Tools)
        {
            sb.Append(tool.Number.ToString(CultureInfo.InvariantCulture));
            foreach (var g in QuestionGroups.Ordered)
            {
                var r = rows.Single(x => x.Tool == tool.Number && x.Group.Code == g.Code);
                sb.Append('\t').Append(Format(r.ErrorsPer100Words))
                  .Append('\t').Append(Format(r.CorrectSentencePct))
                  .Append('\t').Append(r.Words.ToString(CultureInfo.InvariantCulture))
                  .Append('\t').Append(Format(r.MeanSentenceLength));
            }
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
