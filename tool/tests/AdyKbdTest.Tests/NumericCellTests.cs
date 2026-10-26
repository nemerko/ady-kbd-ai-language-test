using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 7: every tool-computed count is written as a genuine numeric cell rather than an
/// inline-string holding the number's text, every blank evaluator-entry cell is written truly
/// empty rather than an empty inline string (§1 -- the bug that made every fresh workbook's
/// formula columns show <c>#VALUE!</c>), the Summary sheet's "Consistency check" block reads the
/// correct-words column (L) instead of the term-choice column (M) (§2), and the duplicate
/// "Marked responses" block is gone (§3).
/// </summary>
[TestClass]
public sealed class NumericCellTests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev7-test-{Guid.NewGuid():N}.xlsx");

    private static WorksheetPart WorksheetOf(SpreadsheetDocument document, string sheetName)
    {
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    // §1: Sentences!F (word count) must be a real numeric cell, not inlineStr holding "2" as text --
    // otherwise SUM/SUMIF treat it as zero and the correct-words formula (F-(H+I+J+K)) evaluates to
    // #VALUE! the moment any of H/I/J/K also holds text instead of a number.
    [TestMethod]
    public void SentencesSheet_F2_IsNumericType()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SentencesSheetName);
            var row2 = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);
            var cell = WorkbookXmlHelpers.GetCell(row2, "F")!;

            Assert.IsNull(cell.InlineString, "F2 must not be an inline string");
            Assert.IsTrue(cell.DataType is null || cell.DataType.Value == CellValues.Number, "F2 must be numeric (t omitted or t=\"n\")");
            Assert.IsTrue(int.TryParse(cell.CellValue?.Text, out var n), "F2's value must parse as a number");
            Assert.IsTrue(n > 0, "the first real sentence should have a positive word count");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1: a blank marking cell (Sentences!H, an error-count entry column) must be written with no
    // type and no value at all -- an empty inlineStr is not the same thing to Excel: COUNTA counts
    // it as filled, and any numeric formula referencing it evaluates to #VALUE! instead of treating
    // it as zero.
    [TestMethod]
    public void SentencesSheet_H2_IsWrittenWithNoValue()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SentencesSheetName);
            var row2 = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);
            var cell = WorkbookXmlHelpers.GetCell(row2, "H")!;

            Assert.IsNull(cell.DataType, "a blank entry cell must carry no type attribute");
            Assert.IsNull(cell.CellValue, "a blank entry cell must carry no <v>");
            Assert.IsNull(cell.InlineString, "a blank entry cell must not be an empty inline string");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1: the same numeric/empty distinction on the Responses sheet (tool-computed counts vs.
    // blank evaluator-entry cells), so the fix is not accidentally Sentences-only.
    [TestMethod]
    public void ResponsesSheet_NumericAndBlankColumns_HaveTheRightCellTypes()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.ResponsesSheetName);
            var row2 = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().First(r => r.RowIndex! == 2u);

            foreach (var column in new[] { "C", "G", "H" })
            {
                var cell = WorkbookXmlHelpers.GetCell(row2, column)!;
                Assert.IsNull(cell.InlineString, $"{column}2 must not be an inline string");
                Assert.IsTrue(cell.DataType is null || cell.DataType.Value == CellValues.Number, $"{column}2 must be numeric");
            }

            foreach (var column in new[] { "I", "J", "K", "L", "M" })
            {
                var cell = WorkbookXmlHelpers.GetCell(row2, column)!;
                Assert.IsNull(cell.DataType, $"{column}2 must carry no type attribute while blank");
                Assert.IsNull(cell.CellValue, $"{column}2 must carry no <v> while blank");
                Assert.IsNull(cell.InlineString, $"{column}2 must not be an empty inline string");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §2: the consistency check now reads L (correct words), not M (term choice).
    [TestMethod]
    public void SummarySheet_ConsistencyCheck_ReferencesColumnL_NotM()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SummarySheetName);

            var rows = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().ToList();
            var labelCell = rows
                .Select(r => WorkbookXmlHelpers.GetCell(r, "A"))
                .FirstOrDefault(c => c is not null && (WorkbookXmlHelpers.Text(c, sstPart)?.Contains("Correct words", StringComparison.Ordinal) ?? false));
            Assert.IsNotNull(labelCell, "consistency-check label row not found");

            var label = WorkbookXmlHelpers.Text(labelCell, sstPart)!;
            StringAssert.Contains(label, "(L)");
            Assert.IsFalse(label.Contains("(M)", StringComparison.Ordinal));

            var row = rows.Single(r => WorkbookXmlHelpers.GetCell(r, "A") == labelCell);
            var formula = WorkbookXmlHelpers.GetCell(row, "B")!.CellFormula!.Text!;
            StringAssert.Contains(formula, "$L:$L");
            Assert.IsFalse(formula.Contains("$M:$M", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §3: "Marked responses" (evet) appears exactly once on the Summary sheet -- inside "Marking
    // progress" -- not duplicated in its own separate block.
    [TestMethod]
    public void SummarySheet_MarkedResponsesFormula_AppearsOnlyOnce()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheetPart = WorksheetOf(document, WorkbookBuilder.SummarySheetName);
            var markedFormula = $"COUNTIF(Responses!$K:$K,\"{CodingSchemeText.Marked[MarkedWord.Evet].DropdownValue}\")";

            var occurrences = worksheetPart.Worksheet.Descendants<Cell>()
                .Count(c => c.CellFormula?.Text == markedFormula);

            Assert.AreEqual(1, occurrences, "the marked-responses COUNTIF must appear exactly once");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
