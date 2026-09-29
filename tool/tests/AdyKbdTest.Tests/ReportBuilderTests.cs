using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

[TestClass]
public sealed class ReportBuilderTests
{
    [TestMethod]
    public void Build_OnAPartiallyMarkedWorkbook_ProducesTablesAndFlagsUnmarkedResponses()
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

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-report-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var analysisDir = Path.Combine(tempDir, "analysis");
        var workbookPath = Path.Combine(tempDir, "test1-marking.xlsx");
        try
        {
            AnalysisExport.WriteJson(Path.Combine(analysisDir, "test1-analysis.json"), records);
            WorkbookBuilder.Build(workbookPath, new WorkbookInput(1, resolved, analyzed));

            MarkFirstResponseAndItsFirstSentence(workbookPath);

            ReportBuilder.Build(workbookPath, analysisDir, tempDir);

            var integrityPath = Path.Combine(tempDir, "test1-integrity-warnings.tsv");
            Assert.IsTrue(File.Exists(integrityPath));
            var integrityText = File.ReadAllText(integrityPath);
            var notMarkedCount = integrityText.Split('\n').Count(l => l.StartsWith("NOT_MARKED", StringComparison.Ordinal));
            Assert.IsTrue(notMarkedCount >= 34, $"expected at least 34 NOT_MARKED warnings, got {notMarkedCount}");

            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "test1-tool-x-category.tsv")));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "test1-language-compliance.tsv")));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "test1-copy-probe.tsv")));

            var langText = File.ReadAllText(Path.Combine(tempDir, "test1-language-compliance.tsv"));
            // header: tool\tfull\tpartial\tnone\tblank ; tool 1 marked "full" once, six responses left blank.
            Assert.IsTrue(langText.Contains("\n1\t1\t0\t0\t6\n", StringComparison.Ordinal), $"unexpected content:\n{langText}");

            var copyProbeText = File.ReadAllText(Path.Combine(tempDir, "test1-copy-probe.tsv"));
            Assert.IsTrue(copyProbeText.Contains("OZGUN_GERI_VERDI", StringComparison.Ordinal) || copyProbeText.Contains("DEGISIKLIGI_YANSITTI", StringComparison.Ordinal) || copyProbeText.Contains("BELIRSIZ", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void MarkFirstResponseAndItsFirstSentence(string workbookPath)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, true);
        var workbookPart = document.WorkbookPart!;

        var responsesSheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.ResponsesSheetName);
        var responsesPart = (WorksheetPart)workbookPart.GetPartById(responsesSheet.Id!);
        var responsesRow2 = responsesPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);
        SetInlineString(responsesRow2, "I", CodingSchemeText.LanguageCompliance[LanguageComplianceWord.Tam].DropdownValue);
        SetInlineString(responsesRow2, "J", CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Karsiladi].DropdownValue);
        SetInlineString(responsesRow2, "K", CodingSchemeText.Marked[MarkedWord.Evet].DropdownValue);
        responsesPart.Worksheet.Save();

        var sentencesSheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.SentencesSheetName);
        var sentencesPart = (WorksheetPart)workbookPart.GetPartById(sentencesSheet.Id!);
        var sentencesRow2 = sentencesPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);
        SetInlineString(sentencesRow2, "G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue);
        sentencesPart.Worksheet.Save();
    }

    private static void SetInlineString(Row row, string column, string value)
    {
        var cell = row.Elements<Cell>().FirstOrDefault(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == column);
        Assert.IsNotNull(cell, $"column {column} cell not found on row 2");
        cell!.DataType = CellValues.InlineString;
        cell.CellValue = null;
        cell.InlineString = new InlineString(new Text(value));
    }
}
