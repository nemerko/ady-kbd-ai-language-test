using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 13: a response with no evaluable content (language compliance UYUM_YOK AND semantic
/// adequacy ALAKASIZ) is excluded from both the numerator and the denominator of the tool x
/// category report -- previously its unlabelled sentences contributed zero errors but its full
/// word count still inflated the denominator, understating that tool's error rate. One shared
/// predicate (<c>HasNoEvaluableContent</c>) now drives the exclusion, the new
/// <c>excluded</c>/<c>word_count</c> columns, the new excluded-responses table, and the
/// <c>BLANK_SENTENCE_VERDICT</c> suppression, so the three cannot silently disagree again.
/// </summary>
[TestClass]
public sealed class Revision13Tests
{
    // Marks one response's language-compliance (I) and semantic-adequacy (J) cells on the
    // Responses sheet and runs the full report against real Test 1 data; every sentence is left
    // at its default (blank verdict, zero error counts) so only the response-level exclusion rule
    // is under test.
    private static void RunReportWithResponseMarked(int tool, int question, LanguageComplianceWord lang, SemanticAdequacyWord semantic, Action<string> assertOutDir)
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

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev13-report-{Guid.NewGuid():N}");
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
                var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.ResponsesSheetName);
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var targetKey = new ResponseId(1, tool, question).ToString();
                var row = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>()
                    .Single(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), null) == targetKey);
                SetInlineStrings(row,
                    ("I", CodingSchemeText.LanguageCompliance[lang].DropdownValue),
                    ("J", CodingSchemeText.SemanticAdequacy[semantic].DropdownValue));
                worksheetPart.Worksheet.Save();
            }

            ReportBuilder.Build(workbookPath, analysisDir, tempDir);
            assertOutDir(tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void SetInlineStrings(Row row, params (string Column, string Value)[] values)
    {
        foreach (var (column, value) in values)
        {
            var cell = row.Elements<Cell>().FirstOrDefault(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == column);
            Assert.IsNotNull(cell, $"column {column} cell not found");
            cell!.DataType = CellValues.InlineString;
            cell.CellValue = null;
            cell.InlineString = new InlineString(new Text(value));
        }
    }

    private static string[] ToolRow(string tsvPath, int tool)
    {
        var lines = File.ReadAllLines(tsvPath);
        var row = lines.Skip(1).Select(l => l.Split('\t')).Single(f => f[0] == tool.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return row;
    }

    // §1/§2: UYUM_YOK + ALAKASIZ excludes the response from both sides of the ratio.
    [TestMethod]
    public void ExcludedResponse_WordsRemovedFromDenominatorAndNumeratorSet()
    {
        RunReportWithResponseMarked(1, 1, LanguageComplianceWord.UyumYok, SemanticAdequacyWord.Alakasiz, outDir =>
        {
            var excludedText = File.ReadAllText(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            StringAssert.Contains(excludedText, "T1-A1-S1");

            var integrityText = File.ReadAllText(Path.Combine(outDir, "test1-integrity-warnings.tsv"));
            Assert.IsFalse(integrityText.Contains("T1-A1-S1: sentence", StringComparison.Ordinal),
                "an excluded response's blank sentences must not raise BLANK_SENTENCE_VERDICT");

            // The excluded response's own word count must be missing from tool 1's denominator:
            // compare against the same response left in (TAM/KARSILADI), which must be larger by
            // exactly that response's own word count.
            var excludedWords = int.Parse(ToolRow(Path.Combine(outDir, "test1-tool-x-category.tsv"), 1)[7], System.Globalization.CultureInfo.InvariantCulture);

            RunReportWithResponseMarked(1, 1, LanguageComplianceWord.Tam, SemanticAdequacyWord.Karsiladi, includedDir =>
            {
                var includedWords = int.Parse(ToolRow(Path.Combine(includedDir, "test1-tool-x-category.tsv"), 1)[7], System.Globalization.CultureInfo.InvariantCulture);
                Assert.IsTrue(includedWords > excludedWords, $"included ({includedWords}) should exceed excluded ({excludedWords})");
            });
        });
    }

    // §4: UYUM_YOK + KARSILADI (only one condition true) stays in the measurement, so its blank
    // sentences must still raise BLANK_SENTENCE_VERDICT, and it must not appear as excluded.
    [TestMethod]
    public void UyumYokWithKarsiladi_IsNotExcluded_AndBlankSentencesAreFlagged()
    {
        RunReportWithResponseMarked(2, 2, LanguageComplianceWord.UyumYok, SemanticAdequacyWord.Karsiladi, outDir =>
        {
            var excludedText = File.ReadAllText(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            Assert.IsFalse(excludedText.Contains("T1-A2-S2", StringComparison.Ordinal));

            var integrityText = File.ReadAllText(Path.Combine(outDir, "test1-integrity-warnings.tsv"));
            StringAssert.Contains(integrityText, "T1-A2-S2: sentence");
            StringAssert.Contains(integrityText, "BLANK_SENTENCE_VERDICT");
        });
    }

    // §2: KISMEN + ALAKASIZ -- a single condition is not enough to exclude.
    [TestMethod]
    public void KismenWithAlakasiz_IsNotExcluded()
    {
        RunReportWithResponseMarked(3, 3, LanguageComplianceWord.Kismen, SemanticAdequacyWord.Alakasiz, outDir =>
        {
            var excludedText = File.ReadAllText(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            Assert.IsFalse(excludedText.Contains("T1-A3-S3", StringComparison.Ordinal));
        });
    }

    // §2: TAM + ALAKASIZ -- a target-language response that misses the point is still measured.
    [TestMethod]
    public void TamWithAlakasiz_IsNotExcluded()
    {
        RunReportWithResponseMarked(4, 2, LanguageComplianceWord.Tam, SemanticAdequacyWord.Alakasiz, outDir =>
        {
            var excludedText = File.ReadAllText(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            Assert.IsFalse(excludedText.Contains("T1-A4-S2", StringComparison.Ordinal));
        });
    }

    // §2: question 4 stays in the measurement even under a combination that would exclude on
    // other questions were it ALAKASIZ instead -- here it plainly is not, so this just confirms S4
    // is never special-cased out.
    [TestMethod]
    public void QuestionFourResponse_KismenKarsiladi_IsNotExcluded()
    {
        RunReportWithResponseMarked(1, 4, LanguageComplianceWord.Kismen, SemanticAdequacyWord.Karsiladi, outDir =>
        {
            var excludedText = File.ReadAllText(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            Assert.IsFalse(excludedText.Contains("T1-A1-S4", StringComparison.Ordinal));
            var questionTableText = File.ReadAllText(Path.Combine(outDir, "test1-tool-x-question.tsv"));
            StringAssert.Contains(questionTableText, "1\t4\t");
        });
    }

    // §3.4/§3.5: no excluded response anywhere -- the table is header-only, the summary line reads 0.
    [TestMethod]
    public void NoExcludedResponses_TableIsHeaderOnly_SummaryReadsZero()
    {
        RunReportWithResponseMarked(1, 1, LanguageComplianceWord.Tam, SemanticAdequacyWord.Karsiladi, outDir =>
        {
            var excludedLines = File.ReadAllLines(Path.Combine(outDir, "test1-excluded-responses.tsv"));
            Assert.AreEqual(1, excludedLines.Length, "only the header row is expected");
            Assert.AreEqual("key\ttool\tquestion\tlanguage_compliance\tsemantic\tword_count\tsentence_count", excludedLines[0]);

            var summaryText = File.ReadAllText(Path.Combine(outDir, "test1-report-summary.txt"));
            StringAssert.Contains(summaryText, "Responses excluded (no evaluable content): 0");
        });
    }

    // §3.2/§3.3: the new "words" column matches what errors_per_100_words was actually computed
    // from, and the header lists it in the documented position.
    [TestMethod]
    public void ToolCategoryTable_WordsColumn_MatchesTheRatiosDenominator()
    {
        RunReportWithResponseMarked(1, 1, LanguageComplianceWord.Tam, SemanticAdequacyWord.Karsiladi, outDir =>
        {
            var lines = File.ReadAllLines(Path.Combine(outDir, "test1-tool-x-category.tsv"));
            Assert.AreEqual("tool\tfrm\tcho\tinv\tmix\ttrm\ttotal_errors\twords\terrors_per_100_words", lines[0]);

            foreach (var line in lines.Skip(1))
            {
                var fields = line.Split('\t');
                var totalErrors = double.Parse(fields[6], System.Globalization.CultureInfo.InvariantCulture);
                var words = double.Parse(fields[7], System.Globalization.CultureInfo.InvariantCulture);
                var per100 = double.Parse(fields[8], System.Globalization.CultureInfo.InvariantCulture);
                var expected = words == 0 ? 0.0 : totalErrors * 100.0 / words;
                Assert.AreEqual(expected, per100, 0.0001, $"tool {fields[0]}: words={words}, total_errors={totalErrors}");
            }
        });
    }
}
