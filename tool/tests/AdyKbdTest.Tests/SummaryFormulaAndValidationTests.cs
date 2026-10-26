using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 6: the Summary sheet's term-choice formula fix (§1), the two "one code per word"
/// custom validations (§2), the FRM/CHO/INV/MIX rename and reordered decision table (§4/§8), free
/// column/row formatting with cell locks intact (§7), the Responses sheet's C2 frozen pane (§7),
/// the widened Info Code column (§8.2), the three new Summary blocks (§9), and the three new
/// integrity checks (§10).
/// </summary>
[TestClass]
public sealed class SummaryFormulaAndValidationTests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev6-test-{Guid.NewGuid():N}.xlsx");

    private static WorksheetPart WorksheetOf(SpreadsheetDocument document, string sheetName)
    {
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    // §1: the Summary sheet's term-choice column must sum Sentences!M (term choice), not the old
    // L (which is now the correct-words formula) -- an unmarked workbook must report zero there.
    [TestMethod]
    public void SummarySheet_TermChoiceFormula_ReferencesColumnM_NotL()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SummarySheetName);
            // The term-choice formula is the only column-I formula on the sheet that sums Sentences
            // by tool number -- found by content rather than a hardcoded row, so the test does not
            // depend on the exact row layout above the Errors-by-tool block's data rows.
            var formulas = worksheetPart.Worksheet.Descendants<Cell>()
                .Where(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == "I" && c.CellFormula is not null)
                .Select(c => c.CellFormula!.Text!)
                .ToList();
            Assert.IsTrue(formulas.Count > 0, "expected at least one formula in column I");

            foreach (var formula in formulas)
            {
                StringAssert.Contains(formula, "$M:$M");
                Assert.IsFalse(formula.Contains("$L:$L", StringComparison.Ordinal), "term choice must not sum the correct-words column (L)");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §2: H:K and M:M each carry a custom validation guarding against over-counting a sentence's words.
    [TestMethod]
    public void SentencesSheet_HasOneCodePerWordCustomValidations()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SentencesSheetName);
            var customValidations = worksheetPart.Worksheet.Descendants<DataValidation>()
                .Where(v => v.Type?.Value == DataValidationValues.Custom).ToList();

            Assert.AreEqual(2, customValidations.Count);

            var errorRange = customValidations.Single(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("H", StringComparison.Ordinal)));
            Assert.AreEqual("SUM($H2:$K2)<=$F2", errorRange.Formula1!.Text);
            Assert.IsTrue(errorRange.AllowBlank?.Value ?? false);

            var termRange = customValidations.Single(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("M", StringComparison.Ordinal)));
            Assert.AreEqual("SUM($H2:$K2)+$M2<=$F2", termRange.Formula1!.Text);
            Assert.IsTrue(termRange.AllowBlank?.Value ?? false);

            foreach (var v in customValidations)
            {
                // Revision 8 §1: the message itself has its own 255-character cap, separate from
                // ErrorTitle's 32-character one -- both enforced centrally now (ValidationMessageLengthTests).
                Assert.IsTrue((v.ErrorTitle?.Value?.Length ?? 0) <= 32, "errorTitle exceeds Excel's 32-character limit");
                Assert.IsTrue((v.Error?.Value?.Length ?? 0) <= 255, "error message exceeds Excel's 255-character limit");
                StringAssert.Contains(v.Error!.Value, "One code per word");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §4: the decision table's row order is FRM, CHO, MIX, INV, TRM -- MIX now comes before INV.
    [TestMethod]
    public void InfoSheet_DecisionTable_MixComesBeforeInv()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).OrderBy(r => r.RowIndex!.Value).ToList();
            string? BCell(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sstPart);

            // Revision 10 §1 made the decision table's Code column match the code dictionary's
            // "CODE: root / root / root" cells exactly (from the same shared source), so the two
            // tables' code cells are no longer distinguishable by text alone -- located by row
            // range (between the "Decision table" and "Code dictionary" headers) instead.
            var startIndex = rows.FindIndex(r => BCell(r) == "Decision table");
            var endIndex = rows.FindIndex(startIndex + 1, r => BCell(r) == "Code dictionary");
            Assert.IsTrue(startIndex >= 0 && endIndex > startIndex, "decision table section not found");

            var codeCells = rows.Skip(startIndex + 1).Take(endIndex - startIndex - 1)
                .Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sstPart))
                .Where(text => text is not null && text != "Code")
                .ToList();

            Assert.AreEqual(5, codeCells.Count, "expected five rows (FRM, CHO, MIX, INV, TRM) in the decision table");
            var order = codeCells.Select(c => c!.Split(':')[0]).ToList();
            CollectionAssert.AreEqual(new[] { "FRM", "CHO", "MIX", "INV", "TRM" }, order);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7: all four sheets keep cell locks (Sheet=true) but leave column/row formatting free.
    [TestMethod]
    public void AllSheets_AllowColumnAndRowFormattingButKeepCellLocks()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            foreach (var name in new[] { WorkbookBuilder.InfoSheetName, WorkbookBuilder.ResponsesSheetName, WorkbookBuilder.SentencesSheetName, WorkbookBuilder.SummarySheetName })
            {
                var worksheetPart = WorksheetOf(document, name);
                var protection = worksheetPart.Worksheet.Descendants<SheetProtection>().Single();

                Assert.IsTrue(protection.Sheet?.Value ?? false, $"{name}: Sheet protection must stay on");
                Assert.IsFalse(protection.FormatColumns?.Value ?? true, $"{name}: FormatColumns must be free");
                Assert.IsFalse(protection.FormatRows?.Value ?? true, $"{name}: FormatRows must be free");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7: the Responses sheet's frozen pane moved from E2 to C2 (only Key + Question stay pinned).
    [TestMethod]
    public void ResponsesSheet_FrozenPane_IsAtC2()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.ResponsesSheetName);
            var pane = worksheetPart.Worksheet.Descendants<Pane>().Single();

            Assert.AreEqual("C2", pane.TopLeftCell?.Value);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §8.2: the Info sheet's Code column is widened to 40 so "INV: invented / uydurma / выдуманное" (36 chars) fits.
    [TestMethod]
    public void InfoSheet_CodeColumnWidth_Is40()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.InfoSheetName);
            var columnA = worksheetPart.Worksheet.Descendants<Column>().Single(c => c.Min?.Value == 1 && c.Max?.Value == 1);

            Assert.AreEqual(40, columnA.Width?.Value);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §9: the three new Summary blocks are present.
    [TestMethod]
    public void SummarySheet_HasTheThreeNewBlocks()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var allText = string.Join("\n", WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SummarySheetName)
                .SelectMany(row => row.Elements<Cell>())
                .Select(c => WorkbookXmlHelpers.Text(c, sstPart))
                .Where(t => t is not null));

            StringAssert.Contains(allText, "Question 7 verdict by tool");
            StringAssert.Contains(allText, "Contact languages by tool");
            StringAssert.Contains(allText, "Marking progress");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §10.1: a sentence marked C1 (correct) with a coded error word is a conflict.
    [TestMethod]
    public void ReportBuilder_C1WithErrorWord_IsFlaggedAsConflict()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row,
                ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue), ("H", "1")),
            assertIntegrity: text => Assert.IsTrue(text.Contains("SENTENCE_VERDICT_CONFLICT", StringComparison.Ordinal), text));
    }

    // §10.1: C2/C3 with zero error words is legitimate (syntax is a sentence-level property) -- no warning.
    [TestMethod]
    public void ReportBuilder_C2WithNoErrorWords_IsNotFlaggedAsConflict()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row,
                ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.YanlisAmaAnlasilir].DropdownValue)),
            assertIntegrity: text => Assert.IsFalse(text.Contains("SENTENCE_VERDICT_CONFLICT", StringComparison.Ordinal), text));
    }

    // §10.1: TRM is excluded from the conflict rule -- C1 with a term choice is legitimate.
    [TestMethod]
    public void ReportBuilder_C1WithTermChoiceOnly_IsNotFlaggedAsConflict()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row,
                ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue), ("M", "1")),
            assertIntegrity: text => Assert.IsFalse(text.Contains("SENTENCE_VERDICT_CONFLICT", StringComparison.Ordinal), text));
    }

    // §10.3: FRM+CHO+INV+MIX+TRM exceeding the word count is an overflow, whether typed or pasted in.
    [TestMethod]
    public void ReportBuilder_CodedWordsExceedingWordCount_IsFlaggedAsOverflow()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("H", "999")),
            assertIntegrity: text => Assert.IsTrue(text.Contains("WORD_COUNT_OVERFLOW", StringComparison.Ordinal), text));
    }

    // §10.3: a pasted-in, non-numeric value in a count column is reported as NON_NUMERIC, not silently read as blank.
    [TestMethod]
    public void ReportBuilder_NonNumericPastedValue_IsFlagged()
    {
        RunReportWithOneSentenceMarked(
            markSentence: row => SetInlineStrings(row, ("H", "n/a")),
            assertIntegrity: text => Assert.IsTrue(text.Contains("NON_NUMERIC", StringComparison.Ordinal), text));
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

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev6-report-{Guid.NewGuid():N}");
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
