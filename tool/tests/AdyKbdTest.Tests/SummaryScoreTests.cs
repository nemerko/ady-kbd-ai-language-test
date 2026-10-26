using System.Globalization;
using System.Text;
using AdyKbdTest.Instrument;

namespace AdyKbdTest.Tests;

/// <summary>Writes the two files <c>akt summary</c> reads for a test's scores, from per-tool (errors, words) pairs.</summary>
internal static class SummaryFixtures
{
    public const string QuestionHeader = "tool\tquestion\tword_count\tevaluated_word_count\terror_word_count\tsentence_count\tcorrect\tfaulty_comprehensible\tfaulty_incomprehensible\texcluded";

    /// <summary>Every question of a tool gets the same (error words, evaluated words), so the tool's test score equals its question score.</summary>
    public static void WriteTest(string dir, int test, Func<int, (int Errors, int Words)> perTool)
    {
        var sb = new StringBuilder(QuestionHeader).Append('\n');
        var scores = new List<(int Tool, int Question, double Score)>();
        foreach (var tool in Enumerable.Range(1, 5))
        {
            var (errors, words) = perTool(tool);
            foreach (var question in Enumerable.Range(1, 7))
            {
                sb.Append(string.Join('\t', tool, question, words, words, errors, 1, 0, 0, 0, "false")).Append('\n');
                scores.Add((tool, question, ScoreRule.QuestionScore(errors, words, excluded: false)));
            }
        }
        File.WriteAllText(Path.Combine(dir, $"test{test}-tool-x-question.tsv"), sb.ToString(), Encoding.UTF8);
        ScoreRule.WriteTestScores(Path.Combine(dir, $"test{test}-scores.tsv"), ScoreRule.ComputeFromQuestionScores(scores));
    }
}

/// <summary>
/// Revision 17: rounding happens only when a value is written. <c>akt summary</c> computes the
/// overall score from the per-question counts at full precision instead of averaging the rounded
/// per-test scores, and treats the per-test scores file as a cross-check only.
/// </summary>
[TestClass]
public sealed class SummaryScoreTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"akt-rev17-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Dictionary<string, string[]> ReadByTool(string path) =>
        File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToDictionary(r => r[0]);

    // Tool 4's test scores are 66.6667, 66.6667, 66.6667 and 33.3333 (e=1,w=3 three times; e=2,w=3):
    // exact mean 58.3333..., but the mean of the four rounded values is 58.33335, which would be written 58.3334.
    private static void WriteRoundingFixture(string dir)
    {
        foreach (var test in new[] { 1, 2, 3 }) SummaryFixtures.WriteTest(dir, test, tool => tool == 4 ? (1, 3) : (0, 10));
        SummaryFixtures.WriteTest(dir, 4, tool => tool == 4 ? (2, 3) : (0, 10));
    }

    // §4.1
    [TestMethod]
    public void OverallScore_IsTheMeanOfTheExactTestScores_NotOfTheRoundedOnes()
    {
        var dir = NewTempDir();
        try
        {
            WriteRoundingFixture(dir);
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g => new TaskTypeRow(t.Number, g, 0, 1, 1, 1, 0))).ToList());

            var testScores = Enumerable.Range(1, 4).Select(t => double.Parse(ReadByTool(Path.Combine(dir, $"test{t}-scores.tsv"))["4"][5], CultureInfo.InvariantCulture)).ToList();
            Assert.AreEqual("58.3334", ScoreRule.Format(testScores.Average()), "the rounded test scores average to 58.33335 -- the trap");

            var result = SummaryBuilder.Build(dir, dir);
            Assert.AreEqual(0, result.Warnings.Count);
            var overall = ReadByTool(Path.Combine(dir, "summary-scores.tsv"))["4"];
            Assert.AreEqual("58.3333", overall[5], "computed from the per-question counts, at full precision");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // §4.2: the per-test columns of summary-scores.tsv are exactly the score columns of the per-test files.
    [TestMethod]
    public void SummaryScores_TestColumns_EqualTheScoresFiles()
    {
        var dir = NewTempDir();
        try
        {
            WriteRoundingFixture(dir);
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g => new TaskTypeRow(t.Number, g, 0, 1, 1, 1, 0))).ToList());
            SummaryBuilder.Build(dir, dir);

            var summary = ReadByTool(Path.Combine(dir, "summary-scores.tsv"));
            foreach (var test in Enumerable.Range(1, 4))
            {
                var perTest = ReadByTool(Path.Combine(dir, $"test{test}-scores.tsv"));
                foreach (var tool in Enumerable.Range(1, 5).Select(t => t.ToString(CultureInfo.InvariantCulture)))
                {
                    Assert.AreEqual(perTest[tool][5], summary[tool][test], $"test {test}, tool {tool}");
                }
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // §4.3: no fallback to the rounded scores file.
    [TestMethod]
    public void MissingQuestionTable_IsAnError()
    {
        var dir = NewTempDir();
        try
        {
            WriteRoundingFixture(dir);
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g => new TaskTypeRow(t.Number, g, 0, 1, 1, 1, 0))).ToList());
            File.Delete(Path.Combine(dir, "test3-tool-x-question.tsv"));

            var ex = Assert.ThrowsException<InvalidOperationException>(() => SummaryBuilder.Build(dir, dir));
            StringAssert.Contains(ex.Message, "test3-tool-x-question.tsv");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // §4.4: a scores file that no longer agrees with the counts is reported, and does not change the result.
    [TestMethod]
    public void EditedScoresFile_RaisesScoreDrift()
    {
        var dir = NewTempDir();
        try
        {
            WriteRoundingFixture(dir);
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g => new TaskTypeRow(t.Number, g, 0, 1, 1, 1, 0))).ToList());
            var path = Path.Combine(dir, "test2-scores.tsv");
            File.WriteAllText(path, File.ReadAllText(path).Replace("66.6667\t66.6667\t66.6667\t66.6667\t66.6667", "66.6667\t66.6667\t66.6667\t66.6667\t12.3456"));

            var result = SummaryBuilder.Build(dir, dir);

            Assert.AreEqual(1, result.Warnings.Count);
            Assert.AreEqual("SCORE_DRIFT", result.Warnings[0].Kind);
            StringAssert.Contains(File.ReadAllText(Path.Combine(dir, "summary-integrity-warnings.tsv")), "SCORE_DRIFT");
            Assert.AreEqual("58.3333", ReadByTool(Path.Combine(dir, "summary-scores.tsv"))["4"][5], "the edited file does not leak into the overall score");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void CleanRun_WritesAnEmptyWarningsFile()
    {
        var dir = NewTempDir();
        try
        {
            WriteRoundingFixture(dir);
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g => new TaskTypeRow(t.Number, g, 0, 1, 1, 1, 0))).ToList());
            SummaryBuilder.Build(dir, dir);

            Assert.AreEqual(1, File.ReadAllLines(Path.Combine(dir, "summary-integrity-warnings.tsv")).Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
