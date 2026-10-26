using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 5: the Info sheet's layout (three languages spread across B/C/D instead of stacked
/// into A), row heights for prose, and the missing/incorrect text items. Most of §1-3's acceptance
/// criteria are explicitly verify-by-opening-the-file (revision 5 §12); what is tested here is the
/// subset that is genuinely structural and machine-checkable -- content location, row height being
/// set at all, and exact text/code corrections.
/// </summary>
[TestClass]
public sealed class InfoSheetLayoutTests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev5-test-{Guid.NewGuid():N}.xlsx");

    // §2: A1 holds the product name exactly once, not stacked three times.
    [TestMethod]
    public void InfoSheet_A1_HasProductNameOnceOnly()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var row1 = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).First(r => r.RowIndex! == 1u);
            var a1 = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row1, "A"), sstPart);

            Assert.AreEqual(AppInfo.DisplayName, a1);
            Assert.IsNull(WorkbookXmlHelpers.GetCell(row1, "B"), "B1 should be empty -- the title has no translation");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1: long prose is spread across B (EN) / C (TR) / D (RU), not stacked into A.
    [TestMethod]
    public void InfoSheet_FramingParagraph_IsInColumnsBCDNotA()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).ToList();

            const string enNeedle = "does not measure translation quality";
            const string trNeedle = "çeviri kalitesini değil";
            const string ruNeedle = "не качество перевода";

            var bHit = rows.Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sstPart)).Any(t => t?.Contains(enNeedle, StringComparison.Ordinal) == true);
            var cHit = rows.Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "C"), sstPart)).Any(t => t?.Contains(trNeedle, StringComparison.Ordinal) == true);
            var dHit = rows.Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "D"), sstPart)).Any(t => t?.Contains(ruNeedle, StringComparison.Ordinal) == true);
            var aHit = rows.Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sstPart)).Any(t => t?.Contains(enNeedle, StringComparison.Ordinal) == true);

            Assert.IsTrue(bHit, "English framing text not found in column B");
            Assert.IsTrue(cHit, "Turkish framing text not found in column C");
            Assert.IsTrue(dHit, "Russian framing text not found in column D");
            Assert.IsFalse(aHit, "framing text must not be in column A");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §3: a prose row's height is set explicitly (not left at the sheet default), and grows with content.
    [TestMethod]
    public void InfoSheet_ProseRows_HaveExplicitHeightAboveDefault()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).ToList();
            var proseRow = rows.First(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sstPart)?.Contains("does not measure translation quality", StringComparison.Ordinal) == true);

            Assert.IsTrue(proseRow.CustomHeight?.Value ?? false);
            Assert.IsTrue((proseRow.Height?.Value ?? 0) > 15, "prose row height should exceed the single-line default");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §5: sub-header rows use a distinct style (and fill) from ordinary code rows.
    [TestMethod]
    public void InfoSheet_SubHeaderStyle_DiffersFromCodeRowStyle()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var sstPart = document.WorkbookPart!.SharedStringTablePart;
            var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).ToList();

            var subHeaderRow = rows.First(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sstPart)?.StartsWith("Sentence verdict", StringComparison.Ordinal) == true);
            var codeRow = rows.First(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sstPart) == "C1");

            var subHeaderStyle = WorkbookXmlHelpers.GetCell(subHeaderRow, "B")!.StyleIndex!.Value;
            var codeRowStyle = WorkbookXmlHelpers.GetCell(codeRow, "B")!.StyleIndex!.Value;

            Assert.AreNotEqual(subHeaderStyle, codeRowStyle);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §6: the dictionary's "Other" row uses the same Russian word as the Sentences!R column header ("Прочие").
    [TestMethod]
    public void CodingSchemeText_ContactLanguagesOther_MatchesSentencesSheetHeader()
    {
        Assert.AreEqual("Прочие", CodingSchemeText.ContactLanguages[ContactLanguage.OTHER].Ru);
    }

    [TestMethod]
    public void InfoSheet_DoesNotContainTheOldMismatchedOtherTranslation()
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

            Assert.IsFalse(allText.Contains("Другой", StringComparison.Ordinal), "the old mismatched translation (\"Другой\") must be gone");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §7: no "уюм йок" / "uyum yok / нет" hybrid phrasing remains; UYUM_YOK (the code) is used instead.
    [TestMethod]
    public void InfoSheet_UsesTheCodeNotAHybridPhrase_ForNoCompliance()
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

            Assert.IsFalse(allText.Contains("уюм йок", StringComparison.Ordinal), "Cyrillic-spelled Turkish (\"уюм йок\") must be gone");
            Assert.IsFalse(allText.Contains("uyum yok / нет", StringComparison.Ordinal), "the old two-language hybrid phrase must be gone");
            Assert.IsTrue(allText.Contains("UYUM_YOK", StringComparison.Ordinal), "the code UYUM_YOK should be used instead");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §8: EVET/HAYIR's English text is "yes"/"no", matching the TR/RU value, not a field description.
    [TestMethod]
    public void CodingSchemeText_Marked_EnglishTextIsYesNo()
    {
        Assert.AreEqual("yes", CodingSchemeText.Marked[MarkedWord.Evet].En);
        Assert.AreEqual("no", CodingSchemeText.Marked[MarkedWord.Hayir].En);
    }

    // §9/§10: the two new rule sentences are present in the Info sheet.
    [TestMethod]
    public void InfoSheet_ContainsTheOneCodePerWordRule()
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

            Assert.IsTrue(allText.Contains("counted under exactly one code", StringComparison.Ordinal));
            Assert.IsTrue(allText.Contains("yalnızca bir koda sayılır", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void InfoSheet_ContainsTheTrmNotInContactLanguagesRule()
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

            Assert.IsTrue(allText.Contains("never written into the contact-language columns", StringComparison.Ordinal));
            Assert.IsTrue(allText.Contains("temas dili sütunlarına yazılmaz", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §4: metadata labels are three-lingual, and "Evaluation method"'s value carries Russian too.
    [TestMethod]
    public void InfoSheet_MetadataLabels_AreThreeLingual()
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

            Assert.IsTrue(allText.Contains("Test / Tur", StringComparison.Ordinal));
            Assert.IsTrue(allText.Contains("Тест / Раунд", StringComparison.Ordinal));
            Assert.IsTrue(allText.Contains("Консенсус", StringComparison.Ordinal), "Evaluation method's value should carry Russian too");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // The workbook must still be schema-valid and column-ordered after this rewrite.
    [TestMethod]
    public void Build_AfterInfoSheetRewrite_IsStillAValidWorkbook()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));

            using var document = SpreadsheetDocument.Open(path, false);
            var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator();
            var errors = validator.Validate(document).ToList();
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));

            foreach (var sheet in document.WorkbookPart!.Workbook.Descendants<Sheet>())
            {
                var worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(sheet.Id!);
                foreach (var row in worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>())
                {
                    var columnIndices = row.Elements<Cell>().Select(c => WorkbookXmlHelpers.ColumnIndexOf(c.CellReference?.Value)).ToList();
                    for (var i = 1; i < columnIndices.Count; i++)
                    {
                        Assert.IsTrue(columnIndices[i] > columnIndices[i - 1], $"sheet {sheet.Name}, row {row.RowIndex}: columns not ascending");
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
