using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.Instrument;

public sealed record SummaryResult(IReadOnlyList<int> TaskTypeTests, IReadOnlyList<int> ScoreTests, IReadOnlyList<IntegrityWarning> Warnings);

/// <summary>
/// <c>akt summary</c> (revision 16 §3.2): the cross-test totals. It reads the per-test files
/// <c>akt report</c> wrote -- never the workbooks -- adds up the <em>raw counts</em>, and recomputes
/// every ratio from the sums; ratios are never averaged. It is a separate command on purpose, so that
/// re-running one test's report does not silently change the overall result: the researcher runs it
/// deliberately.
/// </summary>
public static partial class SummaryBuilder
{
    [GeneratedRegex(@"^test(?<n>\d+)-task-type\.tsv$")]
    private static partial Regex TaskTypeFileRegex();

    [GeneratedRegex(@"^test(?<n>\d+)-scores\.tsv$")]
    private static partial Regex ScoresFileRegex();

    public static SummaryResult Build(string analysisDir, string outDir)
    {
        if (!Directory.Exists(analysisDir))
        {
            throw new InvalidOperationException($"Directory not found: {analysisDir}");
        }

        var taskTypeFiles = FindTestFiles(analysisDir, TaskTypeFileRegex());
        if (taskTypeFiles.Count == 0)
        {
            throw new InvalidOperationException($"No test*-task-type.tsv file found in {analysisDir}; run `akt report` first.");
        }

        Directory.CreateDirectory(outDir);

        var totals = new Dictionary<(int Tool, string Group), int[]>();
        foreach (var (_, path) in taskTypeFiles)
        {
            foreach (var line in File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0))
            {
                var f = line.Split('\t');
                var key = (int.Parse(f[0], CultureInfo.InvariantCulture), f[1]);
                if (!totals.TryGetValue(key, out var sums)) totals[key] = sums = new int[5];
                // error_words, words, sentences, correct, excluded_responses -- the raw counts.
                sums[0] += int.Parse(f[3], CultureInfo.InvariantCulture);
                sums[1] += int.Parse(f[4], CultureInfo.InvariantCulture);
                sums[2] += int.Parse(f[6], CultureInfo.InvariantCulture);
                sums[3] += int.Parse(f[7], CultureInfo.InvariantCulture);
                sums[4] += int.Parse(f[10], CultureInfo.InvariantCulture);
            }
        }

        var rows = new List<TaskTypeRow>();
        foreach (var tool in ToolRegistry.Tools)
        {
            foreach (var group in QuestionGroups.Ordered)
            {
                var s = totals.GetValueOrDefault((tool.Number, group.Code), new int[5]);
                rows.Add(new TaskTypeRow(tool.Number, group, s[0], s[1], s[2], s[3], s[4]));
            }
        }

        var testList = string.Join(',', taskTypeFiles.Select(f => f.Test.ToString(CultureInfo.InvariantCulture)));
        TaskTypeTables.WriteLong(Path.Combine(outDir, "summary-task-type.tsv"), rows, testList);
        TaskTypeTables.WriteWide(Path.Combine(outDir, "summary-main-table.tsv"), rows);

        var scoreFiles = FindTestFiles(analysisDir, ScoresFileRegex());
        var warnings = new List<IntegrityWarning>();
        if (scoreFiles.Count > 0) WriteOverallScores(analysisDir, Path.Combine(outDir, "summary-scores.tsv"), scoreFiles, warnings);

        var warningsText = new StringBuilder("kind\tdetail\n");
        foreach (var w in warnings) warningsText.Append(w.Kind).Append('\t').Append(w.Detail.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
        File.WriteAllText(Path.Combine(outDir, "summary-integrity-warnings.tsv"), warningsText.ToString(), Encoding.UTF8);

        return new SummaryResult(taskTypeFiles.Select(f => f.Test).ToList(), scoreFiles.Select(f => f.Test).ToList(), warnings);
    }

    private static List<(int Test, string Path)> FindTestFiles(string dir, Regex pattern) =>
        Directory.GetFiles(dir)
            .Select(p => (Path: p, Match: pattern.Match(System.IO.Path.GetFileName(p))))
            .Where(x => x.Match.Success)
            .Select(x => (Test: int.Parse(x.Match.Groups["n"].Value, CultureInfo.InvariantCulture), x.Path))
            .OrderBy(x => x.Test)
            .ToList();

    /// <summary>
    /// Revision 17: the overall score is computed from the per-question counts, at full precision --
    /// never from the rounded <c>score</c> column of <c>test{n}-scores.tsv</c>, which is a written
    /// value and not an input. Each test's scores are recomputed by the same <see cref="ScoreRule"/>
    /// code <c>akt report</c> used, from that test's <c>tool-x-question.tsv</c> (the integer
    /// <c>error_word_count</c> and <c>evaluated_word_count</c> and the <c>excluded</c> flag); the
    /// scores file is then only a cross-check (<c>SCORE_DRIFT</c> if it disagrees, which means it was
    /// written by another version of the tool).
    /// </summary>
    private static void WriteOverallScores(string analysisDir, string path, List<(int Test, string Path)> scoreFiles, List<IntegrityWarning> warnings)
    {
        var byTest = new Dictionary<int, IReadOnlyList<ToolScore>>();
        foreach (var (test, scoresPath) in scoreFiles)
        {
            var questionPath = System.IO.Path.Combine(analysisDir, $"test{test}-tool-x-question.tsv");
            if (!File.Exists(questionPath))
            {
                throw new InvalidOperationException(
                    $"test{test}-scores.tsv is present but test{test}-tool-x-question.tsv is not; the overall score is computed from the per-question counts and will not fall back to the rounded scores file. Run `akt report` for test {test} again.");
            }

            // columns: tool, question, word_count, evaluated_word_count, error_word_count, ..., excluded
            var questionScores = File.ReadAllLines(questionPath).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'))
                .Select(f => (
                    Tool: int.Parse(f[0], CultureInfo.InvariantCulture),
                    Question: int.Parse(f[1], CultureInfo.InvariantCulture),
                    Score: ScoreRule.QuestionScore(
                        errorWords: int.Parse(f[4], CultureInfo.InvariantCulture),
                        evaluatedWords: int.Parse(f[3], CultureInfo.InvariantCulture),
                        excluded: f[^1] == "true")))
                .ToList();
            var recomputed = ScoreRule.ComputeFromQuestionScores(questionScores);
            byTest[test] = recomputed;

            foreach (var line in File.ReadAllLines(scoresPath).Skip(1).Where(l => l.Length > 0))
            {
                var f = line.Split('\t');
                var tool = int.Parse(f[0], CultureInfo.InvariantCulture);
                var s = recomputed.Single(x => x.Tool == tool);
                var written = string.Join('\t', f.Skip(1).Take(5));
                var expected = string.Join('\t', ScoreRule.Format(s.Q1To3), ScoreRule.Format(s.Q4), ScoreRule.Format(s.Q5), ScoreRule.Format(s.Q6To7), ScoreRule.Format(s.Score));
                if (written != expected)
                {
                    warnings.Add(new IntegrityWarning("SCORE_DRIFT",
                        $"test {test}, tool {tool}: test{test}-scores.tsv has [{written}] but the per-question counts give [{expected}]; the scores file was probably written by another version of the tool."));
                }
            }
        }

        var overall = ToolRegistry.Tools
            .Select(t => (Tool: t.Number, Score: ScoreRule.Mean(scoreFiles.Select(f => byTest[f.Test].Single(s => s.Tool == t.Number).Score))))
            .ToList();
        var ranks = ScoreRule.Rank(overall);

        var sb = new StringBuilder("tool");
        foreach (var f in scoreFiles) sb.Append("\ttest").Append(f.Test.ToString(CultureInfo.InvariantCulture));
        sb.Append("\tscore\trank\ttie\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var r = ranks.Single(x => x.Tool == tool.Number);
            sb.Append(tool.Number.ToString(CultureInfo.InvariantCulture));
            foreach (var f in scoreFiles) sb.Append('\t').Append(ScoreRule.Format(byTest[f.Test].Single(s => s.Tool == tool.Number).Score));
            sb.Append('\t').Append(ScoreRule.Format(r.Score)).Append('\t').Append(r.Rank).Append('\t').Append(r.Tie ? "true" : "false").Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
