using System.Globalization;
using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 15: the denominator of errors-per-100-words is the words of the sentences that carry a
/// verdict, in responses that are not excluded -- the same set of sentences the errors in the
/// numerator come from. Revision 13 made the numerator and denominator agree at the level of whole
/// responses; this makes them agree at the level of sentences, so the unlabelled, non-target-language
/// sentences of a partly compliant (KISMEN) response no longer inflate the denominator.
/// </summary>
[TestClass]
public sealed class EvaluatedWordCountTests
{
    private sealed record Fixture(string OutDir, ResponseId Target, int TargetWordCount, int[] SentenceWords, Dictionary<int, int> ToolWordCount);

    // Builds a real Test 1 workbook, labels EVERY sentence C1 (so every response is fully "in the
    // denominator" unless a scenario says otherwise), then applies the scenario to one target
    // response and runs the report.
    private static void Run(
        LanguageComplianceWord lang, SemanticAdequacyWord semantic, Func<int, bool> sentenceIsLabelled,
        Action<Fixture> assert, Action<WorksheetPart, string>? tamperSentences = null, bool addOneErrorToFirstLabelled = false)
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

        // First response with at least three sentences, every one of them with a word.
        var target = analyzed.Where(kv => kv.Value.Sentences.Count >= 3 && kv.Value.Sentences.All(s => s.Tokens.Count > 0))
            .OrderBy(kv => kv.Key.Tool).ThenBy(kv => kv.Key.Question).First().Key;
        var sentenceWords = analyzed[target].Sentences.Select(s => s.Tokens.Count).ToArray();
        var toolWords = ToolRegistry.Tools.ToDictionary(t => t.Number, t => records.Where(r => r.Tool == t.Number).Sum(r => r.WordCount));

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev15-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var analysisDir = Path.Combine(tempDir, "analysis");
        var workbookPath = Path.Combine(tempDir, "test1-marking.xlsx");
        try
        {
            AnalysisExport.WriteJson(Path.Combine(analysisDir, "test1-analysis.json"), records);
            WorkbookBuilder.Build(workbookPath, new WorkbookInput(1, resolved, analyzed));

            using (var document = SpreadsheetDocument.Open(workbookPath, true))
            {
                var workbookPart = document.WorkbookPart!;
                WorksheetPart Part(string name) => (WorksheetPart)workbookPart.GetPartById(workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == name).Id!);

                var responsesPart = Part(WorkbookBuilder.ResponsesSheetName);
                var targetRow = responsesPart.Worksheet.Descendants<SheetData>().First().Elements<Row>()
                    .Single(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), null) == target.ToString());
                SetInline(targetRow, ("I", CodingSchemeText.LanguageCompliance[lang].DropdownValue), ("J", CodingSchemeText.SemanticAdequacy[semantic].DropdownValue));
                responsesPart.Worksheet.Save();

                var sentencesPart = Part(WorkbookBuilder.SentencesSheetName);
                var firstLabelledDone = false;
                foreach (var row in sentencesPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().Where(r => r.RowIndex!.Value > 1))
                {
                    var key = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), null);
                    var isTarget = key == target.ToString();
                    var sentenceNo = int.Parse(WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "D"), null)!, CultureInfo.InvariantCulture);
                    if (isTarget && !sentenceIsLabelled(sentenceNo)) continue;

                    SetInline(row, ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue));
                    if (isTarget && addOneErrorToFirstLabelled && !firstLabelledDone)
                    {
                        SetInline(row, ("H", "1"));
                        firstLabelledDone = true;
                    }
                }
                tamperSentences?.Invoke(sentencesPart, target.ToString());
                sentencesPart.Worksheet.Save();
            }

            ReportBuilder.Build(workbookPath, analysisDir, tempDir);
            assert(new Fixture(tempDir, target, analyzed[target].Metrics.WordCount, sentenceWords, toolWords));
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

    private static string[] QuestionRow(string outDir, ResponseId id) =>
        File.ReadAllLines(Path.Combine(outDir, "test1-tool-x-question.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Single(f => f[0] == id.Tool.ToString(CultureInfo.InvariantCulture) && f[1] == id.Question.ToString(CultureInfo.InvariantCulture));

    private static string[] CategoryRow(string outDir, int tool) =>
        File.ReadAllLines(Path.Combine(outDir, "test1-tool-x-category.tsv")).Skip(1).Select(l => l.Split('\t')).Single(f => f[0] == tool.ToString(CultureInfo.InvariantCulture));

    // §4.1: a KISMEN response with labelled and unlabelled sentences contributes only the labelled
    // sentences' words; the raw count is still reported, the unlabelled words are reported on their
    // own line, and the unlabelled sentences are still warned about.
    [TestMethod]
    public void PartlyLabelledResponse_ContributesOnlyLabelledWords()
    {
        Run(LanguageComplianceWord.Kismen, SemanticAdequacyWord.Karsiladi, no => no <= 2, f =>
        {
            var labelled = f.SentenceWords.Take(2).Sum();
            var unlabelled = f.SentenceWords.Skip(2).Sum();

            var row = QuestionRow(f.OutDir, f.Target);
            Assert.AreEqual(f.TargetWordCount, int.Parse(row[2], CultureInfo.InvariantCulture), "word_count is the real word count");
            Assert.AreEqual(labelled, int.Parse(row[3], CultureInfo.InvariantCulture), "evaluated_word_count");
            Assert.AreEqual("false", row[^1]);

            var words = int.Parse(CategoryRow(f.OutDir, f.Target.Tool)[7], CultureInfo.InvariantCulture);
            Assert.AreEqual(f.ToolWordCount[f.Target.Tool] - unlabelled, words);

            var warnings = File.ReadAllText(Path.Combine(f.OutDir, "test1-integrity-warnings.tsv"));
            Assert.AreEqual(f.SentenceWords.Length - 2, warnings.Split('\n').Count(l => l.StartsWith("BLANK_SENTENCE_VERDICT", StringComparison.Ordinal) && l.Contains(f.Target + ":", StringComparison.Ordinal)));
            Assert.IsFalse(warnings.Contains("WORD_COUNT_MISMATCH", StringComparison.Ordinal));

            StringAssert.Contains(File.ReadAllText(Path.Combine(f.OutDir, "test1-report-summary.txt")),
                $"Words in unlabeled sentences of included responses (outside the denominator): {unlabelled}");
        });
    }

    // §4.2: with every sentence labelled, the response contributes its whole word count.
    [TestMethod]
    public void FullyLabelledResponse_ContributesItsWholeWordCount()
    {
        Run(LanguageComplianceWord.Tam, SemanticAdequacyWord.Karsiladi, _ => true, f =>
        {
            var row = QuestionRow(f.OutDir, f.Target);
            Assert.AreEqual(f.TargetWordCount, int.Parse(row[3], CultureInfo.InvariantCulture));
            Assert.AreEqual(f.ToolWordCount[f.Target.Tool], int.Parse(CategoryRow(f.OutDir, f.Target.Tool)[7], CultureInfo.InvariantCulture));
            StringAssert.Contains(File.ReadAllText(Path.Combine(f.OutDir, "test1-report-summary.txt")),
                "(outside the denominator): 0");
        });
    }

    // §4.3: UYUM_YOK + ALAKASIZ is still excluded as in revision 13, and now shows 0 evaluated words.
    [TestMethod]
    public void ExcludedResponse_HasZeroEvaluatedWords()
    {
        Run(LanguageComplianceWord.UyumYok, SemanticAdequacyWord.Alakasiz, _ => false, f =>
        {
            var row = QuestionRow(f.OutDir, f.Target);
            Assert.AreEqual(f.TargetWordCount, int.Parse(row[2], CultureInfo.InvariantCulture));
            Assert.AreEqual(0, int.Parse(row[3], CultureInfo.InvariantCulture));
            Assert.AreEqual("true", row[^1]);
            StringAssert.Contains(File.ReadAllText(Path.Combine(f.OutDir, "test1-excluded-responses.tsv")), f.Target.ToString());
            Assert.AreEqual(f.ToolWordCount[f.Target.Tool] - f.TargetWordCount, int.Parse(CategoryRow(f.OutDir, f.Target.Tool)[7], CultureInfo.InvariantCulture));
            // Excluded responses are not "included", so their unlabelled words are not on the new line.
            StringAssert.Contains(File.ReadAllText(Path.Combine(f.OutDir, "test1-report-summary.txt")), "(outside the denominator): 0");
        });
    }

    // §4.4: a sentence whose F value was changed by hand no longer adds up to the analysis output.
    [TestMethod]
    public void EditedSentenceWordCount_RaisesWordCountMismatch()
    {
        Run(LanguageComplianceWord.Tam, SemanticAdequacyWord.Karsiladi, _ => true, f =>
        {
            var warnings = File.ReadAllText(Path.Combine(f.OutDir, "test1-integrity-warnings.tsv"));
            StringAssert.Contains(warnings, "WORD_COUNT_MISMATCH");
            StringAssert.Contains(warnings, f.Target.ToString());
        }, tamperSentences: (part, key) =>
        {
            var row = part.Worksheet.Descendants<SheetData>().First().Elements<Row>()
                .First(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), null) == key);
            WorkbookXmlHelpers.GetCell(row, "F")!.CellValue = new CellValue("999");
        });
    }

    // §4.5: the rate is total_errors * 100 / words, and words is the sum of the tool's
    // evaluated_word_count over the question table.
    [TestMethod]
    public void RateIsErrorsPer100EvaluatedWords()
    {
        Run(LanguageComplianceWord.Kismen, SemanticAdequacyWord.Karsiladi, no => no <= 2, f =>
        {
            var question = File.ReadAllLines(Path.Combine(f.OutDir, "test1-tool-x-question.tsv")).Skip(1).Select(l => l.Split('\t')).ToList();
            var anyErrors = false;
            foreach (var line in File.ReadAllLines(Path.Combine(f.OutDir, "test1-tool-x-category.tsv")).Skip(1))
            {
                var c = line.Split('\t');
                var totalErrors = double.Parse(c[6], CultureInfo.InvariantCulture);
                var words = double.Parse(c[7], CultureInfo.InvariantCulture);
                var evaluated = question.Where(q => q[0] == c[0]).Sum(q => int.Parse(q[3], CultureInfo.InvariantCulture));
                Assert.AreEqual(evaluated, (int)words, $"tool {c[0]}: words must equal the sum of evaluated_word_count");
                Assert.AreEqual(words == 0 ? 0.0 : totalErrors * 100.0 / words, double.Parse(c[8], CultureInfo.InvariantCulture), 0.0001);
                anyErrors |= totalErrors > 0;
            }
            Assert.IsTrue(anyErrors, "the fixture should put at least one error into the ratio");
        }, addOneErrorToFirstLabelled: true);
    }
}
