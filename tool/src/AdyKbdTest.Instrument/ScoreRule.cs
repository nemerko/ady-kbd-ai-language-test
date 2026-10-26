using System.Globalization;
using System.Text;

namespace AdyKbdTest.Instrument;

/// <summary>One tool's scores in one test (revision 16 §3.3).</summary>
public sealed record ToolScore(int Tool, double Q1To3, double Q4, double Q5, double Q6To7, double Score, int Rank, bool Tie);

/// <summary>
/// The scoring rule, fixed in advance by the researcher (<c>puanlama-kurali-2026-10-26.md</c>) and
/// computed here rather than by hand. This is the single definition: <c>akt report</c> uses it for a
/// test, <c>akt summary</c> for the overall score, and the tests for both.
///
/// 1. A question's score (0-100) is 100 - error words * 100 / evaluated words -- the percentage of
///    error-free words among the evaluated ones. An excluded response (no evaluable content) scores 0;
///    so does a response that is not excluded but has no evaluated words (which should not happen, and
///    is reported as SCORE_NO_WORDS).
/// 2. A group's score is the mean of its questions' scores (the combined group has no score).
/// 3. A test's score is the mean of the four group scores, equally weighted.
/// 4. Rank is 1-5 by score, highest first; equal scores are ordered by tool number (lower first)
///    and flagged with tie = true.
/// </summary>
public static class ScoreRule
{
    public static double QuestionScore(int errorWords, int evaluatedWords, bool excluded) =>
        excluded || evaluatedWords == 0 ? 0.0 : 100.0 - errorWords * 100.0 / evaluatedWords;

    public static double Mean(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0.0 : list.Average();
    }

    /// <summary>Ranks 1..n, highest score first; ties are decided on the 4-decimal value that is written.</summary>
    public static IReadOnlyList<(int Tool, double Score, int Rank, bool Tie)> Rank(IReadOnlyList<(int Tool, double Score)> scores)
    {
        double Shown(double s) => Math.Round(s, 4, MidpointRounding.AwayFromZero);
        // Order by the exact score (lower tool number first when exactly equal); the tie flag is
        // judged on the 4-decimal value that is written, which is what a reader sees.
        var ordered = scores.OrderByDescending(s => s.Score).ThenBy(s => s.Tool).ToList();
        return ordered
            .Select((s, i) => (s.Tool, s.Score, Rank: i + 1, Tie: ordered.Count(o => Shown(o.Score) == Shown(s.Score)) > 1))
            .OrderBy(s => s.Tool)
            .ToList();
    }

    /// <summary>Question scores of one response, exactly as <c>tool-x-question</c> defines its inputs.</summary>
    public static double QuestionScore(ResponseMarking m) =>
        QuestionScore(ReportBuilder.ErrorWordCount(m), ReportBuilder.EvaluatedWordCount(m), ReportBuilder.HasNoEvaluableContent(m));

    public static IReadOnlyList<ToolScore> ComputeForTest(IReadOnlyList<ResponseMarking> markings) =>
        ComputeFromQuestionScores(markings.Select(m => (m.Id.Tool, m.Id.Question, Score: QuestionScore(m))).ToList());

    /// <summary>
    /// Groups, test score, rank -- from unrounded question scores. The one place those steps are
    /// computed, whether the question scores come from a marked workbook (<c>akt report</c>) or from
    /// a <c>tool-x-question</c> table (<c>akt summary</c>, revision 17): rounding happens only when a
    /// value is written, never between steps.
    /// </summary>
    public static IReadOnlyList<ToolScore> ComputeFromQuestionScores(IReadOnlyList<(int Tool, int Question, double Score)> questionScores)
    {
        double GroupScore(int tool, QuestionGroup g) =>
            Mean(questionScores.Where(q => q.Tool == tool && g.Questions.Contains(q.Question)).Select(q => q.Score));

        var perTool = ToolRegistry.Tools.Select(t =>
        {
            var q13 = GroupScore(t.Number, QuestionGroups.Q1To3);
            var q4 = GroupScore(t.Number, QuestionGroups.Q4);
            var q5 = GroupScore(t.Number, QuestionGroups.Q5);
            var q67 = GroupScore(t.Number, QuestionGroups.Q6To7);
            return (Tool: t.Number, Q13: q13, Q4: q4, Q5: q5, Q67: q67, Score: Mean([q13, q4, q5, q67]));
        }).ToList();

        var ranks = Rank(perTool.Select(p => (p.Tool, p.Score)).ToList());
        return perTool.Select(p =>
        {
            var r = ranks.Single(x => x.Tool == p.Tool);
            return new ToolScore(p.Tool, p.Q13, p.Q4, p.Q5, p.Q67, p.Score, r.Rank, r.Tie);
        }).ToList();
    }

    public static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static void WriteTestScores(string path, IReadOnlyList<ToolScore> scores)
    {
        var sb = new StringBuilder("tool\tq1_3\tq4\tq5\tq6_7\tscore\trank\ttie\n");
        foreach (var s in scores)
        {
            sb.Append(string.Join('\t', s.Tool, Format(s.Q1To3), Format(s.Q4), Format(s.Q5), Format(s.Q6To7), Format(s.Score), s.Rank, s.Tie ? "true" : "false")).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
