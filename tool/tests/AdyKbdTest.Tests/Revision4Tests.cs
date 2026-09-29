using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 4: the Sentences sheet's column reorder (term choice out of the error block, contact
/// languages as counts not flags, Note last), the new L/S formulas, the two new integrity checks,
/// and the contact-languages report.
/// </summary>
[TestClass]
public sealed class Revision4Tests
{
    private static WorkbookInput BuildRealInput(int test)
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
        var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
        foreach (var r in resolved)
        {
            foreach (var q in r.Selected.Questions)
            {
                analyzed[new ResponseId(test, r.Selected.Tool, q.Number)] = TextAnalyzer.Analyze(q.AnswerText, profile);
            }
        }
        return new WorkbookInput(test, resolved, analyzed);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev4-test-{Guid.NewGuid():N}.xlsx");

    // §1: L is F-(H+I+J+K) -- term choice (M) must not appear in it, fixing revision 3's mistaken
    // formula which subtracted the old L (= term choice at the time).
    [TestMethod]
    public void SentencesSheet_CorrectWordsFormula_ExcludesTermChoice()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var row2 = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SentencesSheetName).First(r => r.RowIndex! == 2u);
            var formula = WorkbookXmlHelpers.GetCell(row2, "L")!.CellFormula!.Text!;

            Assert.AreEqual("F2-(H2+I2+J2+K2)", formula);
            Assert.IsFalse(formula.Contains('M', StringComparison.Ordinal), "term choice (M) must not enter the correct-words formula");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1: S is N+O+P+Q+R.
    [TestMethod]
    public void SentencesSheet_TotalContactLanguageWordsFormula_IsCorrect()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var row2 = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SentencesSheetName).First(r => r.RowIndex! == 2u);
            var formula = WorkbookXmlHelpers.GetCell(row2, "S")!.CellFormula!.Text!;

            Assert.AreEqual("N2+O2+P2+Q2+R2", formula);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1/§8: every contact-language and error-count column is a whole-number entry field, none of
    // them a dropdown/list; the formula columns (L, S) carry none; Note (T) carries none either.
    [TestMethod]
    public void SentencesSheet_NoContactLanguageColumnHasAListValidation()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sheet = document.WorkbookPart!.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.SentencesSheetName);
            var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
            var validations = worksheetPart.Worksheet.Descendants<DataValidation>().ToList();

            foreach (var col in new[] { "H", "I", "J", "K", "M", "N", "O", "P", "Q", "R" })
            {
                // H, I, J, K and M each also fall inside one of revision 6's H:K / M "one code per
                // word" custom-formula ranges (§2), so filtering on Type=Whole here specifically
                // finds the per-column whole-number rule, not that overlapping custom one.
                var v = validations.SingleOrDefault(v => v.Type?.Value == DataValidationValues.Whole
                    && v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith(col, StringComparison.Ordinal)));
                Assert.IsNotNull(v, $"column {col} has no whole-number validation");
            }

            Assert.IsFalse(validations.Any(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("L", StringComparison.Ordinal))), "L is a formula column, no validation expected");
            Assert.IsFalse(validations.Any(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("S", StringComparison.Ordinal))), "S is a formula column, no validation expected");
            Assert.IsFalse(validations.Any(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("T", StringComparison.Ordinal))), "T (Note) is free text, no validation expected");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1: Note is the very last column on both sheets.
    [TestMethod]
    public void SentencesSheet_NoteIsTheLastColumn()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var headerRow = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SentencesSheetName).First(r => r.RowIndex! == 1u);
            var cells = headerRow.Elements<Cell>().OrderBy(c => WorkbookXmlHelpers.ColumnIndexOf(c.CellReference?.Value)).ToList();
            var lastCell = cells[^1];

            Assert.AreEqual("T", WorkbookXmlHelpers.ColumnOf(lastCell.CellReference?.Value));
            StringAssert.Contains(WorkbookXmlHelpers.Text(lastCell, sstPart), "Note");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void ResponsesSheet_NoteIsTheLastColumn()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var headerRow = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.ResponsesSheetName).First(r => r.RowIndex! == 1u);
            var cells = headerRow.Elements<Cell>().OrderBy(c => WorkbookXmlHelpers.ColumnIndexOf(c.CellReference?.Value)).ToList();
            var lastCell = cells[^1];

            Assert.AreEqual("M", WorkbookXmlHelpers.ColumnOf(lastCell.CellReference?.Value));
            StringAssert.Contains(WorkbookXmlHelpers.Text(lastCell, sstPart), "Note");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §8: frozen pane on Sentences is still at G2 after the reorder.
    [TestMethod]
    public void SentencesSheet_FrozenPane_IsStillAtG2()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sheet = document.WorkbookPart!.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.SentencesSheetName);
            var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
            var pane = worksheetPart.Worksheet.Descendants<Pane>().Single();

            Assert.AreEqual("G2", pane.TopLeftCell?.Value);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // WorkbookReader's computed CorrectWords/TotalContactLanguageWords match the sheet's own formula intent.
    [TestMethod]
    public void SentenceMarking_ComputedCorrectWords_ExcludesTermChoice()
    {
        var marking = new SentenceMarking
        {
            SentenceNo = 1, SentenceText = "x", WordCount = 10,
            Verdict = SentenceVerdict.YanlisAmaAnlasilir, VerdictBlank = false,
            WrongForm = 1, WrongWord = 1, Invented = 0, CodeMixing = 1, TermChoice = 2,
            KbdCount = 0, RusCount = 1, TurCount = 0, EngCount = 0, OtherCount = 0,
            Note = null, NonNumericColumns = [],
        };

        // 10 - (1+1+0+1) = 7; term choice (2) is not subtracted.
        Assert.AreEqual(7, marking.CorrectWords);
        Assert.AreEqual(1, marking.TotalContactLanguageWords);
    }

    // §2: K filled and S != K -> CONTACT_LANGUAGE_MISMATCH.
    [TestMethod]
    public void ReportBuilder_ContactLanguageMismatch_IsFlagged()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("K", "3"), ("N", "1")), // K=3 but N+O+P+Q+R=1
            assertIntegrity: text => Assert.IsTrue(text.Contains("CONTACT_LANGUAGE_MISMATCH", StringComparison.Ordinal), text));
    }

    // §2: K and contact-language columns both blank -> no mismatch warning.
    [TestMethod]
    public void ReportBuilder_BothBlank_NoContactLanguageMismatch()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue)),
            assertIntegrity: text => Assert.IsFalse(text.Contains("CONTACT_LANGUAGE_MISMATCH", StringComparison.Ordinal), text));
    }

    // §4 item 4: a negative count is flagged, not treated as zero.
    [TestMethod]
    public void ReportBuilder_NegativeCount_IsFlagged()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("H", "-1")),
            assertIntegrity: text => Assert.IsTrue(text.Contains("NEGATIVE_COUNT", StringComparison.Ordinal), text));
    }

    // §5: the new contact-languages report is written with the right shape and totals.
    [TestMethod]
    public void ReportBuilder_ContactLanguagesTable_IsWrittenWithCorrectTotal()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("N", "2"), ("O", "1"), ("K", "3")),
            assertIntegrity: null,
            assertOutDir: outDir =>
            {
                var path = Path.Combine(outDir, "test1-contact-languages.tsv");
                Assert.IsTrue(File.Exists(path));
                var text = File.ReadAllText(path).TrimStart('﻿');
                Assert.AreEqual("tool\tkbd\trus\ttur\teng\tother\ttotal", text.Split('\n')[0].TrimEnd('\r'));
                var tool1Line = text.Split('\n').Single(l => l.StartsWith("1\t", StringComparison.Ordinal));
                Assert.AreEqual("1\t2\t1\t0\t0\t0\t3", tool1Line.TrimEnd('\r'));
            });
    }

    private static void RunReportWithOneSentenceMarked(Action<Row> markSentence, Action<string>? assertIntegrity, Action<string>? assertOutDir = null)
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

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev4-report-{Guid.NewGuid():N}");
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
                var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.SentencesSheetName);
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var row2 = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);
                markSentence(row2);
                worksheetPart.Worksheet.Save();
            }

            ReportBuilder.Build(workbookPath, analysisDir, tempDir);

            if (assertIntegrity is not null)
            {
                var integrityText = File.ReadAllText(Path.Combine(tempDir, "test1-integrity-warnings.tsv"));
                assertIntegrity(integrityText);
            }
            assertOutDir?.Invoke(tempDir);
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
}
