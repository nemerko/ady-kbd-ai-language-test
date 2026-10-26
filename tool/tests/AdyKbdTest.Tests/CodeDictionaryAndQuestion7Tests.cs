using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 3: code dictionary codes, decision-table codes, the TRM explanation, and the
/// question-7 verdict column. Revision 3's "open other language" free-text field
/// (Sentences!S/T) was itself replaced by revision 4's numeric R/S columns (§1 there); the tests
/// that covered it were removed here and superseded by <c>SentencesSheetLayoutTests</c>. The question-7
/// verdict column's letter changed from M to L in revision 4 (§3); the tests below were updated
/// in place rather than moved, since the behaviour they check (dropdown only editable on question
/// 7's rows) is unchanged.
/// </summary>
[TestClass]
public sealed class CodeDictionaryAndQuestion7Tests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev3-test-{Guid.NewGuid():N}.xlsx");

    // §1: every coding-scheme option carries a Code, and it is exactly the string stored by
    // ParseDropdownValue's round trip -- i.e. the code an evaluator sees in the Info sheet
    // dictionary corresponds 1:1 to the dropdown value actually stored in a cell.
    [TestMethod]
    public void CodingSchemeText_EveryEntry_HasANonEmptyCode()
    {
        void AssertAllCoded<TEnum>(IReadOnlyDictionary<TEnum, CodeEntry> map) where TEnum : struct, Enum
        {
            foreach (var (key, entry) in map)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Code), $"{typeof(TEnum).Name}.{key} has no code");
                Assert.AreEqual(key, CodingSchemeText.ParseDropdownValue(map, entry.DropdownValue),
                    $"{typeof(TEnum).Name}.{key}: parsing its own dropdown value back did not return itself");
            }
        }

        AssertAllCoded(CodingSchemeText.SentenceVerdicts);
        AssertAllCoded(CodingSchemeText.WordCodes);
        AssertAllCoded(CodingSchemeText.ContactLanguages);
        AssertAllCoded(CodingSchemeText.LanguageCompliance);
        AssertAllCoded(CodingSchemeText.SemanticAdequacy);
        AssertAllCoded(CodingSchemeText.Marked);
        AssertAllCoded(CodingSchemeText.Question7Verdicts);
    }

    // Revision 6 §8.1 renamed the four English-rooted codes (BIC->FRM, SEC->CHO, UYD->INV, KAR->MIX);
    // TRM was already English-rooted and stayed put.
    [TestMethod]
    public void CodingSchemeText_WordCodes_MatchTheEnglishRootedCodes()
    {
        Assert.AreEqual("C1", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].Code);
        Assert.AreEqual("FRM", CodingSchemeText.WordCodes[WordCode.FRM].Code);
        Assert.AreEqual("CHO", CodingSchemeText.WordCodes[WordCode.CHO].Code);
        Assert.AreEqual("INV", CodingSchemeText.WordCodes[WordCode.INV].Code);
        Assert.AreEqual("MIX", CodingSchemeText.WordCodes[WordCode.MIX].Code);
        Assert.AreEqual("TRM", CodingSchemeText.WordCodes[WordCode.TRM].Code);
        Assert.AreEqual("EVET", CodingSchemeText.Marked[MarkedWord.Evet].Code);
        Assert.AreEqual("HAYIR", CodingSchemeText.Marked[MarkedWord.Hayir].Code);
    }

    // §1/§2: the Info sheet's code dictionary and decision table are both four columns wide and
    // column A actually contains a known code (spot-checked, since the sheet's layout is otherwise free text).
    [TestMethod]
    public void InfoSheet_ContainsCodesInColumnA()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var codesSeen = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName)
                .Select(row => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), sstPart))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToHashSet(StringComparer.Ordinal);

            // Revision 6 §8.2 (word-code dictionary) and revision 10 §1 (decision table, made to
            // match) both spell out each code's root ("TRM: term / terim / термин") rather than a
            // bare, standalone cell -- so these are checked as substrings, not exact matches.
            Assert.IsTrue(codesSeen.Any(c => c?.Contains("TRM", StringComparison.Ordinal) == true), "TRM code not found anywhere in column A of the Info sheet");
            Assert.IsTrue(codesSeen.Any(c => c?.Contains("FRM", StringComparison.Ordinal) == true), "FRM code not found anywhere in column A of the Info sheet");
            Assert.IsTrue(codesSeen.Contains("C1"));
            Assert.IsTrue(codesSeen.Contains("KBD"));
            Assert.IsTrue(codesSeen.Contains(CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].Code));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §3: the TRM explanation, with its three-way example, is present in the Info sheet.
    [TestMethod]
    public void InfoSheet_ContainsTrmExplanationWithThreeWayExample()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var allText = string.Join("\n", WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName)
                .SelectMany(row => row.Elements<Cell>())
                .Select(c => WorkbookXmlHelpers.Text(c, sstPart))
                .Where(t => t is not null));

            Assert.IsTrue(allText.Contains("TRM", StringComparison.Ordinal));
            Assert.IsTrue(allText.Contains("искусственнэ интеллект", StringComparison.Ordinal), "adapted form (TRM example) missing");
            Assert.IsTrue(allText.Contains("искусственный интеллект", StringComparison.Ordinal), "source-form (MIX example) missing");
            Assert.IsTrue(allText.Contains("искусственнай интеллект", StringComparison.Ordinal), "non-standard form (INV example) missing");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §5: Responses sheet has a Question 7 verdict column, dropdown-validated, filled in and
    // unlocked only on question 7's rows; locked and blank everywhere else.
    [TestMethod]
    public void ResponsesSheet_Question7VerdictColumn_OnlyEditableOnQuestion7Rows()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var stylesPart = document.WorkbookPart.WorkbookStylesPart!;
            var cellFormats = stylesPart.Stylesheet.CellFormats!.Elements<CellFormat>().ToList();

            bool IsLocked(uint styleIndex)
            {
                var format = cellFormats[(int)styleIndex];
                return format.Protection?.Locked?.Value ?? true; // default (absent) is locked
            }

            foreach (var row in WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.ResponsesSheetName))
            {
                if (row.RowIndex is null || row.RowIndex.Value == 1) continue;
                var keyText = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), sstPart)!;
                var id = ResponseId.Parse(keyText);
                var lCell = WorkbookXmlHelpers.GetCell(row, "L")!;
                var lText = WorkbookXmlHelpers.Text(lCell, sstPart);

                if (id.Question == 7)
                {
                    Assert.IsFalse(IsLocked(lCell.StyleIndex!.Value), $"{id}: L should be unlocked on a question 7 row");
                }
                else
                {
                    Assert.IsTrue(IsLocked(lCell.StyleIndex!.Value), $"{id}: L should be locked on a non-question-7 row");
                    Assert.IsTrue(string.IsNullOrEmpty(lText), $"{id}: L should be blank on a non-question-7 row");
                }
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void ResponsesSheet_Question7VerdictValidation_OnlyCoversQuestion7Range()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sheet = document.WorkbookPart!.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.ResponsesSheetName);
            var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
            var lValidation = worksheetPart.Worksheet.Descendants<DataValidation>()
                .Single(v => v.SequenceOfReferences!.Items.Any(i => i.Value!.StartsWith("L", StringComparison.Ordinal)));

            var sstPart = document.WorkbookPart.SharedStringTablePart;
            var q7Rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart, WorkbookBuilder.ResponsesSheetName)
                .Where(r => r.RowIndex?.Value > 1)
                .Where(r => ResponseId.Parse(WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sstPart)!).Question == 7)
                .Select(r => r.RowIndex!.Value)
                .ToList();

            Assert.AreEqual($"L{q7Rows.Min()}:L{q7Rows.Max()}", lValidation.SequenceOfReferences!.InnerText);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §5 items 4-5: report output and integrity warning for the new column.
    [TestMethod]
    public void ReportBuilder_Question7Verdict_IsWrittenAndFlaggedWhenMissing()
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

        var tempDir = Path.Combine(Path.GetTempPath(), $"akt-rev3-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var analysisDir = Path.Combine(tempDir, "analysis");
        var workbookPath = Path.Combine(tempDir, "test1-marking.xlsx");
        try
        {
            AnalysisExport.WriteJson(Path.Combine(analysisDir, "test1-analysis.json"), records);
            WorkbookBuilder.Build(workbookPath, new WorkbookInput(1, resolved, analyzed));

            // Mark one question-7 row's Değerlendirildi=evet but leave the verdict blank -> should warn.
            MarkQuestion7RowAsEvaluatedButUnverdicted(workbookPath);

            ReportBuilder.Build(workbookPath, analysisDir, tempDir);

            var q7Path = Path.Combine(tempDir, "test1-q7-verdict.tsv");
            Assert.IsTrue(File.Exists(q7Path));
            var q7Text = File.ReadAllText(q7Path);
            Assert.IsTrue(q7Text.Contains("(blank)", StringComparison.Ordinal));

            var integrityText = File.ReadAllText(Path.Combine(tempDir, "test1-integrity-warnings.tsv"));
            Assert.IsTrue(integrityText.Contains("Q7_VERDICT_MISSING", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void MarkQuestion7RowAsEvaluatedButUnverdicted(string workbookPath)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, true);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.ResponsesSheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sstPart = workbookPart.SharedStringTablePart;

        var q7Row = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>()
            .First(r => r.RowIndex?.Value > 1 &&
                ResponseId.Parse(WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sstPart)!).Question == 7);

        SetInlineString(q7Row, "K", CodingSchemeText.Marked[MarkedWord.Evet].DropdownValue);
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
