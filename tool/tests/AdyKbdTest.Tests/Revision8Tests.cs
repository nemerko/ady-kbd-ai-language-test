using System.Reflection;
using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 8: the "one code per word" custom validations' message is shortened below Excel's own
/// (schema-invisible) 255-character <c>error</c> limit, which the revision 6 message exceeded
/// (394 characters) and which <c>OpenXmlValidator</c> cannot catch -- and <c>WorkbookBuilder</c>
/// now enforces all four <see cref="DataValidation"/> text-length limits centrally, at generation
/// time, so this class of bug cannot recur silently.
/// </summary>
[TestClass]
public sealed class Revision8Tests
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

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev8-test-{Guid.NewGuid():N}.xlsx");

    private static WorksheetPart WorksheetOf(SpreadsheetDocument document, string sheetName)
    {
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    // The real, generated custom-validation messages must sit under both of Excel's limits --
    // this is what actually failed to open in Excel before this pass (revision 6's message was
    // 394 characters, over the 255-character cap OpenXmlValidator does not check).
    [TestMethod]
    public void SentencesSheet_CustomValidationText_IsWithinExcelsLimits()
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
            foreach (var v in customValidations)
            {
                Assert.IsTrue((v.ErrorTitle?.Value?.Length ?? 0) <= 32, "ErrorTitle exceeds Excel's 32-character limit");
                Assert.IsTrue((v.Error?.Value?.Length ?? 0) <= 255, "Error exceeds Excel's 255-character limit");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §1's "kalıcı önlem": the shared DataValidation append point rejects an over-limit ErrorTitle
    // or Error at generation time, rather than silently writing a file Excel will refuse to open.
    // Reached via reflection since it is (deliberately) an internal implementation detail, not part
    // of WorkbookBuilder's public surface.
    [DataTestMethod]
    [DataRow(nameof(DataValidation.ErrorTitle), 33, DisplayName = "ErrorTitle one over the 32-character limit")]
    [DataRow(nameof(DataValidation.Error), 256, DisplayName = "Error one over the 255-character limit")]
    [DataRow(nameof(DataValidation.PromptTitle), 33, DisplayName = "PromptTitle one over the 32-character limit")]
    [DataRow(nameof(DataValidation.Prompt), 256, DisplayName = "Prompt one over the 255-character limit")]
    public void AppendValidation_TextOverLimit_ThrowsAtGenerationTime(string field, int length)
    {
        var validation = new DataValidation
        {
            Type = DataValidationValues.Custom,
            SequenceOfReferences = new DocumentFormat.OpenXml.ListValue<DocumentFormat.OpenXml.StringValue> { InnerText = "H2:H2" },
        };
        validation.Append(new Formula1("TRUE"));
        var overLongText = new string('a', length);
        switch (field)
        {
            case nameof(DataValidation.ErrorTitle): validation.ErrorTitle = overLongText; break;
            case nameof(DataValidation.Error): validation.Error = overLongText; break;
            case nameof(DataValidation.PromptTitle): validation.PromptTitle = overLongText; break;
            case nameof(DataValidation.Prompt): validation.Prompt = overLongText; break;
        }

        var ex = Assert.ThrowsException<TargetInvocationException>(() => InvokeAppendValidation(new DataValidations(), validation));
        Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
        StringAssert.Contains(ex.InnerException!.Message, field);
    }

    // Sanity check that the guard does not reject text that is exactly at the limit or unset --
    // it must only reject text that actually exceeds Excel's limit, not merely long text in general.
    [TestMethod]
    public void AppendValidation_TextAtOrUnderLimit_DoesNotThrow()
    {
        var validation = new DataValidation
        {
            Type = DataValidationValues.Custom,
            ErrorTitle = new string('a', 32),
            Error = new string('b', 255),
            SequenceOfReferences = new DocumentFormat.OpenXml.ListValue<DocumentFormat.OpenXml.StringValue> { InnerText = "H2:H2" },
        };
        validation.Append(new Formula1("TRUE"));

        InvokeAppendValidation(new DataValidations(), validation);
    }

    private static void InvokeAppendValidation(DataValidations validations, DataValidation validation)
    {
        var method = typeof(WorkbookBuilder).GetMethod("AppendValidation", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("WorkbookBuilder.AppendValidation not found -- has it been renamed?");
        method.Invoke(null, [validations, validation]);
    }
}
