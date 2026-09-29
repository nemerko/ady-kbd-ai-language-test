using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;

namespace AdyKbdTest.Tests;

[TestClass]
public sealed class WorkbookTests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-test-{Guid.NewGuid():N}.xlsx");

    [TestMethod]
    public void Build_RealTest1Data_ProducesAValidOpenXmlWorkbook()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            Assert.IsTrue(File.Exists(path));

            using var document = SpreadsheetDocument.Open(path, false);
            var validator = new OpenXmlValidator();
            var errors = validator.Validate(document).ToList();
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));

            var sheetNames = document.WorkbookPart!.Workbook.Descendants<Sheet>().Select(s => s.Name!.Value!).ToList();
            CollectionAssert.AreEqual(new[] { "Info", "Responses", "Sentences", "Summary" }, sheetNames);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7 test 5: every row's cells must be in strictly ascending column order, and rows in
    // strictly ascending row-number order -- exactly the condition whose violation made Excel
    // silently discard sheet content before this was fixed.
    [TestMethod]
    public void Build_EveryRow_HasCellsInAscendingColumnOrderAndRowsInAscendingOrder()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            foreach (var sheet in document.WorkbookPart!.Workbook.Descendants<Sheet>())
            {
                var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
                var rows = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().ToList();

                uint? previousRowIndex = null;
                foreach (var row in rows)
                {
                    Assert.IsNotNull(row.RowIndex, $"sheet {sheet.Name}: a row has no index");
                    if (previousRowIndex is not null)
                    {
                        Assert.IsTrue(row.RowIndex!.Value > previousRowIndex.Value,
                            $"sheet {sheet.Name}: row {row.RowIndex.Value} is not after row {previousRowIndex.Value}");
                    }
                    previousRowIndex = row.RowIndex!.Value;

                    var columnIndices = row.Elements<Cell>()
                        .Select(c => WorkbookXmlHelpers.ColumnIndexOf(c.CellReference?.Value))
                        .ToList();
                    for (var i = 1; i < columnIndices.Count; i++)
                    {
                        Assert.IsTrue(columnIndices[i] > columnIndices[i - 1],
                            $"sheet {sheet.Name}, row {row.RowIndex.Value}: cell columns are not strictly ascending ({string.Join(",", columnIndices)})");
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7 test 6: the interactive layer must actually be present, not merely coded and skipped.
    [TestMethod]
    public void Build_InteractiveLayer_IsPresentInEverySheetThatNeedsIt()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var worksheets = document.WorkbookPart!.WorksheetParts.Select(p => p.Worksheet).ToList();

            var dataValidationCount = worksheets.Sum(w => w.Descendants<DataValidation>().Count());
            var sheetProtectionCount = worksheets.Sum(w => w.Descendants<SheetProtection>().Count());
            var paneCount = worksheets.Sum(w => w.Descendants<Pane>().Count());
            var colCount = worksheets.Sum(w => w.Descendants<Column>().Count());

            Assert.IsTrue(dataValidationCount > 0, "no data validations found");
            Assert.IsTrue(sheetProtectionCount > 0, "no sheet protection found");
            Assert.IsTrue(paneCount > 0, "no frozen panes found");
            Assert.IsTrue(colCount > 0, "no column width definitions found");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7 test 7: the Sentences sheet's "Correct words" column must be a live formula, not a stored constant.
    [TestMethod]
    public void Build_SentencesSheet_CorrectWordsColumnIsAFormula()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var lCells = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SentencesSheetName)
                .Where(r => r.RowIndex?.Value > 1)
                .Select(r => WorkbookXmlHelpers.GetCell(r, "L"))
                .Where(c => c is not null)
                .ToList();

            Assert.IsTrue(lCells.Count > 0);
            Assert.IsTrue(lCells.All(c => c!.CellFormula is not null), "at least one L (correct words) cell has no formula");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Build_ThenRead_RoundTripsWithoutMarkedValues()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            var markings = WorkbookReader.Read(path);

            Assert.AreEqual(35, markings.Count); // 5 tools x 7 questions
            Assert.IsTrue(markings.All(m => m.Marked is null));
            Assert.IsTrue(markings.Any(m => m.Sentences.Count > 0));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7 test 8: an unmarked workbook is safe to overwrite.
    [TestMethod]
    public void BuildWithOverwriteProtection_ExistingUnmarkedFile_IsOverwritten()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            var firstWriteTime = File.GetLastWriteTimeUtc(path);
            Thread.Sleep(20);

            var result = WorkbookBuilder.BuildWithOverwriteProtection(path, BuildRealInput(1));

            Assert.AreEqual(WorkbookWriteOutcome.OverwrittenEmpty, result.Outcome);
            Assert.AreEqual(path, result.WrittenPath);
            Assert.IsTrue(File.GetLastWriteTimeUtc(path) >= firstWriteTime);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7 test 9: a marked workbook is never overwritten; a numbered alternate is written instead
    // and the original file's content is untouched.
    [TestMethod]
    public void BuildWithOverwriteProtection_ExistingMarkedFile_IsPreservedAndNumberedAlternateWritten()
    {
        var path = TempPath();
        var numberedPath = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}-2.xlsx");
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            MarkOneResponseRow(path);
            var originalBytes = File.ReadAllBytes(path);

            var result = WorkbookBuilder.BuildWithOverwriteProtection(path, BuildRealInput(1));

            Assert.AreEqual(WorkbookWriteOutcome.KeptExistingWroteNumbered, result.Outcome);
            Assert.AreEqual(path, result.PreservedExistingPath);
            Assert.AreEqual(numberedPath, result.WrittenPath);
            Assert.IsTrue(File.Exists(numberedPath));
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path), "the marked file's bytes must be untouched");

            var markings = WorkbookReader.Read(path);
            Assert.IsTrue(markings.Any(m => m.Marked == MarkedWord.Evet));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(numberedPath)) File.Delete(numberedPath);
        }
    }

    [TestMethod]
    public void HasAnyMarks_FreshlyBuiltWorkbook_IsFalse()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            Assert.IsFalse(WorkbookBuilder.HasAnyMarks(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void HasAnyMarks_AfterMarkingOneCell_IsTrue()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            MarkOneResponseRow(path);
            Assert.IsTrue(WorkbookBuilder.HasAnyMarks(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    internal static void MarkOneResponseRow(string workbookPath)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, true);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.ResponsesSheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sheetData = worksheetPart.Worksheet.Descendants<SheetData>().First();
        var row2 = sheetData.Elements<Row>().First(r => r.RowIndex! == 2u);

        SetInlineString(row2, "K", CodingSchemeText.Marked[MarkedWord.Evet].DropdownValue);

        worksheetPart.Worksheet.Save();
    }

    private static void SetInlineString(Row row, string column, string value)
    {
        var cell = row.Elements<Cell>().FirstOrDefault(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == column);
        Assert.IsNotNull(cell, $"column {column} cell not found");
        cell!.DataType = CellValues.InlineString;
        cell.CellValue = null;
        cell.InlineString = new InlineString(new Text(value));
    }
}
