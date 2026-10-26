using System.Globalization;
using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 16: the main table by task type (question groups), the score tables, and the
/// <c>akt summary</c> cross-test totals. Nothing that already existed changes; the new tables use
/// the revision 13-15 definitions (<c>HasNoEvaluableContent</c>, <c>EvaluatedSentences</c>,
/// <c>EvaluatedWordCount</c>) rather than redefining them.
/// </summary>
[TestClass]
public sealed class TaskTypeAndScoreTests
{
    // Excluded response of the fixture: tool 1's answer to S4 (UYUM_YOK + ALAKASIZ), sentences unlabelled.
    private static readonly ResponseId Excluded = new(1, 1, 4);

    // Real Test 1 data; every sentence of every response labelled C1 and carrying one error word
    // (so every ratio is non-trivial), except the excluded response, whose sentences stay unlabelled.
    private static void RunFixture(Action<string> assert)
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 1);
        var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
        var records = new List<AnalysisRecord>();
        foreach (var r in resolved)
        {
            foreach (var q in r.Selected.Questions)
            {
                var id = new ResponseId(1, r.Selected.Tool, q.Number);
                var a = TextAnalyzer.Analyze(q.AnswerText, profile);
                analyzed[id] = a;
                records.Add(AnalysisRecord.From(id, a));
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev16-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var workbookPath = Path.Combine(tempDir, "test1-marking.xlsx");
        try
        {
            AnalysisExport.WriteJson(Path.Combine(tempDir, "analysis", "test1-analysis.json"), records);
            WorkbookBuilder.Build(workbookPath, new WorkbookInput(1, resolved, analyzed));

            using (var document = SpreadsheetDocument.Open(workbookPath, true))
            {
                var workbookPart = document.WorkbookPart!;
                WorksheetPart Part(string name) => (WorksheetPart)workbookPart.GetPartById(workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == name).Id!);

                var responses = Part(WorkbookBuilder.ResponsesSheetName);
                foreach (var row in responses.Worksheet.Descendants<SheetData>().First().Elements<Row>().Where(r => r.RowIndex!.Value > 1))
                {
                    if (WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), null) != Excluded.ToString()) continue;
                    SetInline(row,
                        ("I", CodingSchemeText.LanguageCompliance[LanguageComplianceWord.UyumYok].DropdownValue),
                        ("J", CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Alakasiz].DropdownValue));
                }
                responses.Worksheet.Save();

                var sentences = Part(WorkbookBuilder.SentencesSheetName);
                foreach (var row in sentences.Worksheet.Descendants<SheetData>().First().Elements<Row>().Where(r => r.RowIndex!.Value > 1))
                {
                    if (WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), null) == Excluded.ToString()) continue;
                    SetInline(row, ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue), ("H", "1"));
                }
                sentences.Worksheet.Save();
            }

            ReportBuilder.Build(workbookPath, Path.Combine(tempDir, "analysis"), tempDir);
            assert(tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void SetInline(Row row, params (string Column, string Value)[] values)
    {
        foreach (var (column, value) in values)
        {
            var cell = row.Elements<Cell>().Single(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == column);
            cell.DataType = CellValues.InlineString;
            cell.CellValue = null;
            cell.InlineString = new InlineString(new Text(value));
        }
    }

    private static List<string[]> Rows(string path) =>
        File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    // §4.1
    [TestMethod]
    public void QuestionGroups_MapQuestionsAndKeepTheirOrder()
    {
        Assert.AreEqual("q1_3", QuestionGroups.TaskTypeOf(1).Code);
        Assert.AreEqual("q1_3", QuestionGroups.TaskTypeOf(2).Code);
        Assert.AreEqual("q1_3", QuestionGroups.TaskTypeOf(3).Code);
        Assert.AreEqual("q4", QuestionGroups.TaskTypeOf(4).Code);
        Assert.AreEqual("q5", QuestionGroups.TaskTypeOf(5).Code);
        Assert.AreEqual("q6_7", QuestionGroups.TaskTypeOf(6).Code);
        Assert.AreEqual("q6_7", QuestionGroups.TaskTypeOf(7).Code);
        CollectionAssert.AreEqual(new[] { "q1_3", "q4", "q5", "q6_7", "all" }, QuestionGroups.Ordered.Select(g => g.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "S1-S3", "S4", "S5", "S6-S7", "S1-S7" }, QuestionGroups.Ordered.Select(g => g.QuestionsLabel).ToArray());
    }

    // §4.2, §4.3, §4.4, §4.5, §4.6 on one fixture.
    [TestMethod]
    public void TaskTypeTable_IsConsistentWithTheExistingTables()
    {
        RunFixture(dir =>
        {
            var task = Rows(Path.Combine(dir, "test1-task-type.tsv"));
            Assert.AreEqual(25, task.Count, "5 tools x 5 groups");
            CollectionAssert.AreEqual(new[] { "q1_3", "q4", "q5", "q6_7", "all", "q1_3" }, task.Take(6).Select(r => r[1]).ToArray());

            var category = Rows(Path.Combine(dir, "test1-tool-x-category.tsv"));
            var verdicts = Rows(Path.Combine(dir, "test1-sentence-verdicts.tsv"));
            foreach (var tool in Enumerable.Range(1, 5))
            {
                var all = task.Single(r => r[0] == tool.ToString(CultureInfo.InvariantCulture) && r[1] == "all");
                var cat = category.Single(r => r[0] == all[0]);
                // error_words is FRM+CHO+INV+MIX, i.e. the category table's total_errors (TRM is not an error).
                Assert.AreEqual(Int(cat[6]), Int(all[3]), $"tool {tool} error_words");
                Assert.AreEqual(Int(cat[7]), Int(all[4]), $"tool {tool} words");
                Assert.AreEqual(double.Parse(cat[8], CultureInfo.InvariantCulture), double.Parse(all[5], CultureInfo.InvariantCulture), 0.0001, $"tool {tool} rate");

                var v = verdicts.Single(r => r[0] == all[0]);
                Assert.AreEqual(Int(v[1]), Int(all[7]), $"tool {tool} correct");
                Assert.AreEqual(Int(v[1]) + Int(v[2]) + Int(v[3]), Int(all[6]), $"tool {tool} sentences");

                // The four groups add up to the combined row, raw count by raw count.
                var groups = task.Where(r => r[0] == all[0] && r[1] != "all").ToList();
                foreach (var column in new[] { 3, 4, 6, 7, 10 })
                {
                    Assert.AreEqual(Int(all[column]), groups.Sum(g => Int(g[column])), $"tool {tool} column {column}");
                }
            }

            // The excluded response: counted in its own group, and its words and sentences nowhere.
            var q4Tool1 = task.Single(r => r[0] == "1" && r[1] == "q4");
            Assert.AreEqual(1, Int(q4Tool1[10]));
            Assert.AreEqual(0, Int(q4Tool1[4]), "no words");
            Assert.AreEqual(0, Int(q4Tool1[6]), "no sentences");

            // A group with no words: every ratio cell is empty, never 0.
            Assert.AreEqual("", q4Tool1[5]);
            Assert.AreEqual("", q4Tool1[8]);
            Assert.AreEqual("", q4Tool1[9]);

            // The wide table follows the same order and shows the empty cells.
            var main = File.ReadAllLines(Path.Combine(dir, "test1-main-table.tsv"));
            var header = main[0].Split('\t');
            Assert.AreEqual("tool", header[0]);
            CollectionAssert.AreEqual(
                new[] { "q1_3_errors_per_100_words", "q1_3_correct_sentence_pct", "q1_3_words", "q1_3_mean_sentence_length", "q4_errors_per_100_words" },
                header.Skip(1).Take(5).ToArray());
            Assert.AreEqual(21, header.Length);
            Assert.AreEqual("all_mean_sentence_length", header[^1]);
            var tool1 = main[1].Split('\t');
            Assert.AreEqual("", tool1[5], "q4 rate cell of tool 1 is empty");
            Assert.AreEqual("0", tool1[7], "q4 words cell of tool 1 is 0");
        });
    }

    // §4.5 (counted properly): only the one excluded response is counted as excluded.
    [TestMethod]
    public void ExcludedResponse_IsCountedExactlyOnce_InTheCombinedRowToo()
    {
        RunFixture(dir =>
        {
            var task = Rows(Path.Combine(dir, "test1-task-type.tsv"));
            Assert.AreEqual(1, task.Where(r => r[1] == "all").Sum(r => Int(r[10])));
            Assert.AreEqual(1, task.Where(r => r[1] != "all").Sum(r => Int(r[10])));
        });
    }

    // §4.8
    [TestMethod]
    public void Score_GroupIsTheMeanOfItsQuestions_TestIsTheMeanOfFourGroups()
    {
        var questionScores = new[]
        {
            ScoreRule.QuestionScore(errorWords: 2, evaluatedWords: 10, excluded: false),
            ScoreRule.QuestionScore(errorWords: 0, evaluatedWords: 7, excluded: false),
            ScoreRule.QuestionScore(errorWords: 3, evaluatedWords: 9, excluded: true),
        };
        CollectionAssert.AreEqual(new[] { 80.0, 100.0, 0.0 }, questionScores);
        Assert.AreEqual(60.0, ScoreRule.Mean(questionScores));
        Assert.AreEqual(90.0, ScoreRule.Mean([60.0, 100.0, 100.0, 100.0]));
        Assert.AreEqual(0.0, ScoreRule.QuestionScore(0, 0, excluded: false), "no evaluated words scores 0");
    }

    // §4.9
    [TestMethod]
    public void Rank_EqualScores_AreOrderedByToolNumber_AndFlaggedAsTies()
    {
        var ranks = ScoreRule.Rank([(1, 50.0), (2, 70.0), (3, 50.0), (4, 60.0), (5, 70.0)]);

        // Order: tool 2 (70), tool 5 (70), tool 4 (60), tool 1 (50), tool 3 (50); listed here by tool number.
        CollectionAssert.AreEqual(new[] { 4, 1, 5, 3, 2 }, ranks.OrderBy(r => r.Tool).Select(r => r.Rank).ToArray());
        Assert.AreEqual(1, ranks.Single(r => r.Tool == 2).Rank, "tied 70s: the lower tool number first");
        Assert.AreEqual(2, ranks.Single(r => r.Tool == 5).Rank);
        Assert.IsTrue(ranks.Where(r => r.Tool is 2 or 5 or 1 or 3).All(r => r.Tie));
        Assert.IsFalse(ranks.Single(r => r.Tool == 4).Tie);
        Assert.AreEqual(3, ranks.Single(r => r.Tool == 4).Rank);
    }

    // The scores file the report writes carries the documented columns, one row per tool.
    [TestMethod]
    public void ScoresFile_HasTheDocumentedShape_AndExcludedQuestionScoresZero()
    {
        RunFixture(dir =>
        {
            var lines = File.ReadAllLines(Path.Combine(dir, "test1-scores.tsv"));
            Assert.AreEqual("tool\tq1_3\tq4\tq5\tq6_7\tscore\trank\ttie", lines[0].TrimStart('﻿'));
            var rows = Rows(Path.Combine(dir, "test1-scores.tsv"));
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual("0", rows.Single(r => r[0] == "1")[2], "tool 1's only S4 answer is excluded: group score 0");
            CollectionAssert.AreEquivalent(new[] { "1", "2", "3", "4", "5" }, rows.Select(r => r[6]).ToArray());
        });
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"akt-rev16-sum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static IReadOnlyList<TaskTypeRow> SparseRows(int errorWords, int words) =>
        ToolRegistry.Tools.SelectMany(t => QuestionGroups.Ordered.Select(g =>
            t.Number == 1 && g.Code == "q1_3" ? new TaskTypeRow(t.Number, g, errorWords, words, 4, 1, 0) : new TaskTypeRow(t.Number, g, 0, 0, 0, 0, 0))).ToList();

    // §4.7: the ratio of the sums, not the mean of the ratios.
    [TestMethod]
    public void Summary_RecomputesRatiosFromRawSums()
    {
        var dir = NewTempDir();
        try
        {
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), SparseRows(errorWords: 1, words: 10)); // 10 per 100 words
            TaskTypeTables.WriteLong(Path.Combine(dir, "test2-task-type.tsv"), SparseRows(errorWords: 3, words: 90)); // 3.3333 per 100 words

            var result = SummaryBuilder.Build(dir, dir);
            CollectionAssert.AreEqual(new[] { 1, 2 }, result.TaskTypeTests.ToArray());

            var lines = File.ReadAllLines(Path.Combine(dir, "summary-task-type.tsv"));
            Assert.AreEqual("tests\t" + TaskTypeTables.LongHeader, lines[0].TrimStart('﻿'));
            var tool1Q13 = lines.Skip(1).Select(l => l.Split('\t')).Single(r => r[1] == "1" && r[2] == "q1_3");
            Assert.AreEqual("1,2", tool1Q13[0]);
            Assert.AreEqual("4", tool1Q13[4]);
            Assert.AreEqual("100", tool1Q13[5]);
            Assert.AreEqual("4", tool1Q13[6], "4 errors / 100 words = 4 per 100 -- the mean of 10 and 3.3333 would be 6.6667");
            Assert.AreEqual("8", tool1Q13[7], "sentences are summed too");
            Assert.AreEqual("", lines.Skip(1).Select(l => l.Split('\t')).Single(r => r[1] == "2" && r[2] == "q4")[6], "a group with no words has an empty ratio");

            Assert.IsTrue(File.Exists(Path.Combine(dir, "summary-main-table.tsv")));
            Assert.IsFalse(File.Exists(Path.Combine(dir, "summary-scores.tsv")), "no scores files, so no summary-scores.tsv");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Summary_WithNoFiles_IsAnError()
    {
        var dir = NewTempDir();
        try
        {
            Assert.ThrowsException<InvalidOperationException>(() => SummaryBuilder.Build(dir, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // §4.10
    [TestMethod]
    public void Summary_OverallScoreIsTheMeanOfTheTestScores_AndRankFollowsIt()
    {
        var dir = NewTempDir();
        try
        {
            TaskTypeTables.WriteLong(Path.Combine(dir, "test1-task-type.tsv"), SparseRows(1, 10));
            // Since revision 17 the summary computes scores from the per-question counts, so the fixture
            // supplies counts that give these test scores: (errors, words) = 80, 60, 70, 50, 90 / 60, 100, 70, 80, 50.
            var test1 = new Dictionary<int, (int, int)> { [1] = (1, 5), [2] = (2, 5), [3] = (3, 10), [4] = (1, 2), [5] = (1, 10) };
            var test2 = new Dictionary<int, (int, int)> { [1] = (2, 5), [2] = (0, 7), [3] = (3, 10), [4] = (1, 5), [5] = (1, 2) };
            SummaryFixtures.WriteTest(dir, 1, tool => test1[tool]);
            SummaryFixtures.WriteTest(dir, 2, tool => test2[tool]);

            var result = SummaryBuilder.Build(dir, dir);
            CollectionAssert.AreEqual(new[] { 1, 2 }, result.ScoreTests.ToArray());

            var lines = File.ReadAllLines(Path.Combine(dir, "summary-scores.tsv"));
            Assert.AreEqual("tool\ttest1\ttest2\tscore\trank\ttie", lines[0].TrimStart('﻿'));
            var rows = lines.Skip(1).Select(l => l.Split('\t')).ToDictionary(r => r[0]);
            Assert.AreEqual("70", rows["1"][3]);
            Assert.AreEqual("80", rows["2"][3]);
            Assert.AreEqual("70", rows["3"][3]);
            Assert.AreEqual("65", rows["4"][3]);
            Assert.AreEqual("70", rows["5"][3]);
            Assert.AreEqual("1", rows["2"][4]);
            // tools 1, 3 and 5 are tied on 70: ordered by tool number, flagged.
            Assert.AreEqual("2", rows["1"][4]);
            Assert.AreEqual("3", rows["3"][4]);
            Assert.AreEqual("4", rows["5"][4]);
            Assert.AreEqual("5", rows["4"][4]);
            Assert.AreEqual("true", rows["1"][5]);
            Assert.AreEqual("false", rows["2"][5]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
