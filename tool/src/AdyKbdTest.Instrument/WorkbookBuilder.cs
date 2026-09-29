using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Instrument;

/// <summary>
/// Builds the marking workbook (revision 1, §4): four sheets -- Bilgi/Info/Информация,
/// Yanıtlar/Responses/Ответы, Cümleler/Sentences/Предложения, Özet/Summary/Сводка. Built in the
/// order the project docs prescribe -- data and headers, then column widths and frozen panes, then
/// dropdowns, then sheet protection, then summary formulas -- because raw OpenXML is easy to get
/// subtly wrong in a way Excel will not report, only silently "repair" (discard) on open.
///
/// Every cell within a row is written in strictly ascending column order (see
/// <see cref="AddRow"/>): this is the one place that guarantee is made, because Excel silently
/// drops a row's cell data -- and, empirically, much more than just that row -- when it is not.
/// </summary>
public static partial class WorkbookBuilder
{
    public const string InfoSheetName = "Info";
    public const string ResponsesSheetName = "Responses";
    public const string SentencesSheetName = "Sentences";
    public const string SummarySheetName = "Summary";

    // Style indices; must match the order CellFormats are appended in BuildStylesheet.
    private const uint StyleHeader = 1;
    private const uint StyleLockedWrap = 2;
    private const uint StyleLockedCenter = 3;
    private const uint StyleUnlockedCenter = 4;
    private const uint StyleUnlockedWrap = 5;
    private const uint StyleSubHeader = 6;

    // ---- public entry points ---------------------------------------------------------------

    /// <summary>
    /// Writes the workbook, refusing to silently overwrite one that already carries evaluator
    /// marks (revision 1, §3): no file at <paramref name="desiredPath"/> -&gt; write it; a file
    /// with no marks -&gt; overwrite it; a file with marks -&gt; leave it untouched and write a
    /// numbered alternate ("-2", "-3", ...) instead. There is deliberately no way to force an
    /// overwrite of marked work.
    /// </summary>
    public static WorkbookWriteResult BuildWithOverwriteProtection(string desiredPath, WorkbookInput input)
    {
        if (!File.Exists(desiredPath))
        {
            Build(desiredPath, input);
            return new WorkbookWriteResult(desiredPath, WorkbookWriteOutcome.Created, null);
        }

        if (!HasAnyMarks(desiredPath))
        {
            Build(desiredPath, input);
            return new WorkbookWriteResult(desiredPath, WorkbookWriteOutcome.OverwrittenEmpty, null);
        }

        var numberedPath = NextAvailableNumberedPath(desiredPath);
        Build(numberedPath, input);
        return new WorkbookWriteResult(numberedPath, WorkbookWriteOutcome.KeptExistingWroteNumbered, desiredPath);
    }

    private static string NextAvailableNumberedPath(string desiredPath)
    {
        var dir = Path.GetDirectoryName(desiredPath) ?? "";
        var stem = Path.GetFileNameWithoutExtension(desiredPath);
        var ext = Path.GetExtension(desiredPath);
        for (var n = 2; ; n++)
        {
            var candidate = Path.Combine(dir, $"{stem}-{n}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    /// <summary>
    /// True if the Responses or Sentences sheet of the workbook at <paramref name="path"/> has
    /// anything written in an evaluator-editable column. Used to decide whether an existing file
    /// is safe to overwrite.
    /// </summary>
    public static bool HasAnyMarks(string path)
    {
        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart!;
        var sstPart = workbookPart.SharedStringTablePart;

        bool AnyNonEmpty(string sheetName, params string[] columns)
        {
            foreach (var row in WorkbookXmlHelpers.ReadRows(workbookPart, sheetName))
            {
                if (row.RowIndex?.Value is 1) continue; // header
                foreach (var col in columns)
                {
                    var text = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, col), sstPart);
                    if (!string.IsNullOrWhiteSpace(text)) return true;
                }
            }
            return false;
        }

        // Sentences!L and !S are formula cells (correct words, contact-language total): every
        // freshly generated, never-marked row already has a cached "0" in them, so checking those
        // columns here would make every fresh workbook look "already marked". Only genuine entry
        // columns count.
        return AnyNonEmpty(ResponsesSheetName, "I", "J", "K", "L", "M")
            || AnyNonEmpty(SentencesSheetName, "G", "H", "I", "J", "K", "M", "N", "O", "P", "Q", "R", "T");
    }

    /// <summary>Writes the workbook unconditionally, overwriting anything already at <paramref name="outputPath"/>.</summary>
    public static void Build(string outputPath, WorkbookInput input)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = BuildStylesheet();
        stylesPart.Stylesheet.Save();

        var infoPart = workbookPart.AddNewPart<WorksheetPart>();
        var responsesPart = workbookPart.AddNewPart<WorksheetPart>();
        var sentencesPart = workbookPart.AddNewPart<WorksheetPart>();
        var summaryPart = workbookPart.AddNewPart<WorksheetPart>();

        BuildInfoSheet(infoPart, input);
        BuildResponsesSheet(responsesPart, input);
        BuildSentencesSheet(sentencesPart, input);
        BuildSummarySheet(summaryPart);

        var sheets = new Sheets();
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(infoPart), SheetId = 1, Name = InfoSheetName });
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(responsesPart), SheetId = 2, Name = ResponsesSheetName });
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(sentencesPart), SheetId = 3, Name = SentencesSheetName });
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(summaryPart), SheetId = 4, Name = SummarySheetName });
        workbookPart.Workbook.Append(sheets);
        workbookPart.Workbook.Append(new CalculationProperties { FullCalculationOnLoad = true });

        workbookPart.Workbook.Save();
    }

    // ---- styles -----------------------------------------------------------------------------

    private static Stylesheet BuildStylesheet()
    {
        var fonts = new Fonts(
            new Font(new FontSize { Val = 10 }, new FontName { Val = "Segoe UI" }),
            new Font(new Bold(), new FontSize { Val = 10 }, new FontName { Val = "Segoe UI" }))
        { Count = 2 };

        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFD9D9D9" }) { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFFFF2CC" }) { PatternType = PatternValues.Solid }),
            // Lighter than the header fill (D9D9D9): a sub-header must read as a distinct, lesser
            // level than a section header, not the same weight (revision 5 §5).
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFF2F2F2" }) { PatternType = PatternValues.Solid }))
        { Count = 5 };

        var borders = new Borders(
            new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()),
            new Border(
                new LeftBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
                new RightBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
                new TopBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
                new BottomBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
                new DiagonalBorder()))
        { Count = 2 };

        var cellStyleFormats = new CellStyleFormats(new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 };

        var cellFormats = new CellFormats(
            // 0: default, locked
            new CellFormat { FontId = 0, FillId = 0, BorderId = 1, FormatId = 0, ApplyBorder = true },
            // 1: header - bold, filled, wrapped, locked
            new CellFormat
            {
                FontId = 1, FillId = 2, BorderId = 1, FormatId = 0,
                ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center },
            },
            // 2: wrapped, locked (long text: question/answer/sentence text)
            new CellFormat
            {
                FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
                ApplyBorder = true, ApplyAlignment = true,
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top },
            },
            // 3: centered, locked (short tool-filled values)
            new CellFormat
            {
                FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
                ApplyBorder = true, ApplyAlignment = true,
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center },
            },
            // 4: centered, unlocked (dropdowns, counts)
            new CellFormat
            {
                FontId = 0, FillId = 3, BorderId = 1, FormatId = 0,
                ApplyBorder = true, ApplyFill = true, ApplyProtection = true, ApplyAlignment = true,
                Protection = new Protection { Locked = false },
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center },
            },
            // 5: wrapped, unlocked (free-text Note column)
            new CellFormat
            {
                FontId = 0, FillId = 3, BorderId = 1, FormatId = 0,
                ApplyBorder = true, ApplyFill = true, ApplyProtection = true, ApplyAlignment = true,
                Protection = new Protection { Locked = false },
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top },
            },
            // 6: sub-header - bold, light-filled, locked; one visual step below the full header (1),
            // so a group label in a dictionary reads as distinct from the header above it and the
            // code rows below it, without competing with the header for weight (revision 5 §5).
            new CellFormat
            {
                FontId = 1, FillId = 4, BorderId = 1, FormatId = 0,
                ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
                Alignment = new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Left },
            })
        { Count = 6 };

        return new Stylesheet(fonts, fills, borders, cellStyleFormats, cellFormats);
    }

    // ---- shared cell/row helpers --------------------------------------------------------------

    /// <summary>
    /// Appends a row, sorting its cells into ascending column order first. This is the single
    /// place that guarantee is made (revision 1, §1): callers may build a row's cells in whatever
    /// order is convenient and this will still emit valid, Excel-safe XML.
    /// </summary>
    private static void AddRow(SheetData sheetData, uint rowIndex, IEnumerable<Cell> cells, double? height = null)
    {
        var row = new Row { RowIndex = rowIndex };
        if (height is { } h)
        {
            row.Height = h;
            row.CustomHeight = true;
        }
        foreach (var cell in cells.OrderBy(c => WorkbookXmlHelpers.ColumnIndexOf(c.CellReference?.Value)))
        {
            row.Append(cell);
        }
        sheetData.Append(row);
    }

    /// <summary>
    /// Rough row height (in points) for a wrapped cell holding <paramref name="text"/> at
    /// <paramref name="columnWidthChars"/> characters wide: counts, per explicit line, how many
    /// wrapped lines that line needs at this width, sums them across all three languages' longest,
    /// and converts to points at ~15pt/line plus one line of padding (revision 5 §3 -- "kabaca").
    /// Not exact (Excel's own wrapping depends on the actual font and character widths, not a flat
    /// chars-per-line estimate), but this is explicitly a verify-by-opening-the-file item, not a
    /// unit-testable one; see tool/README.md.
    /// </summary>
    private static double EstimateWrappedRowHeight(double columnWidthChars, params string[] texts) =>
        EstimateWrappedRowHeight(texts.Select(t => (t, columnWidthChars)).ToArray());

    /// <summary>
    /// Same estimate as the single-width overload, but for a row whose cells sit in columns of
    /// different widths (a header row mixing a 20-char code column with a 55-char text column,
    /// say): each cell is wrapped at its own column's width, and the row takes the tallest result.
    /// </summary>
    private static double EstimateWrappedRowHeight(params (string Text, double ColumnWidthChars)[] cells)
    {
        const double PointsPerLine = 15;
        var maxLines = 1;
        foreach (var (text, columnWidthChars) in cells)
        {
            var lines = 0;
            foreach (var line in text.Split('\n'))
            {
                lines += Math.Max(1, (int)Math.Ceiling(line.Length / columnWidthChars));
            }
            maxLines = Math.Max(maxLines, lines);
        }
        return (maxLines + 1) * PointsPerLine;
    }

    private static Cell Cell(string column, uint rowIndex, string value, uint styleIndex) =>
        new()
        {
            CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
            DataType = CellValues.InlineString,
            StyleIndex = styleIndex,
            InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve }),
        };

    private static Cell FormulaCell(string column, uint rowIndex, string formula, uint styleIndex) =>
        new()
        {
            CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
            StyleIndex = styleIndex,
            CellFormula = new CellFormula(formula),
            CellValue = new CellValue("0"),
            DataType = CellValues.Number,
        };

    /// <summary>
    /// A genuinely numeric cell (revision 7 §1): every tool-computed count (word count, sentence
    /// number, tool number, ...) must be <c>t="n"</c> with the number itself in <c>&lt;v&gt;</c>,
    /// not an <c>inlineStr</c> holding the number's text. Excel's own formulas (<c>SUM</c>,
    /// <c>SUMIF</c>, the correct-words/contact-language formulas, revision 6 §2's custom
    /// validations) silently treat text as zero or as "always greater", which is how the bug this
    /// fixes went undetected until real marking exposed it.
    /// </summary>
    private static Cell NumberCell(string column, uint rowIndex, int value, uint styleIndex) =>
        new()
        {
            CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
            StyleIndex = styleIndex,
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
        };

    /// <summary>
    /// A truly empty evaluator-entry cell (revision 7 §1): only its style/lock, no <c>t</c> and no
    /// value at all -- <c>&lt;c r="H2" s="4"/&gt;</c>. An empty <c>inlineStr</c> (what every blank
    /// marking cell held before this fix) is not the same thing to Excel: <c>COUNTA</c> counts it as
    /// filled, and a numeric formula referencing it evaluates to <c>#VALUE!</c> instead of treating
    /// it as zero.
    /// </summary>
    private static Cell EmptyCell(string column, uint rowIndex, uint styleIndex) =>
        new()
        {
            CellReference = column + rowIndex.ToString(CultureInfo.InvariantCulture),
            StyleIndex = styleIndex,
        };

    private static void AddListValidation(DataValidations validations, IReadOnlyList<string> sqrefParts, IReadOnlyList<string> values)
    {
        if (sqrefParts.Count == 0) return;
        var formula = "\"" + string.Join(",", values) + "\"";
        var validation = new DataValidation
        {
            Type = DataValidationValues.List,
            AllowBlank = true,
            ShowErrorMessage = true,
            ErrorStyle = DataValidationErrorStyleValues.Stop,
            SequenceOfReferences = new ListValue<StringValue> { InnerText = string.Join(" ", sqrefParts) },
        };
        validation.Append(new Formula1(formula));
        AppendValidation(validations, validation);
    }

    private static void AddWholeNumberValidation(DataValidations validations, IReadOnlyList<string> sqrefParts)
    {
        if (sqrefParts.Count == 0) return;
        var validation = new DataValidation
        {
            Type = DataValidationValues.Whole,
            Operator = DataValidationOperatorValues.GreaterThanOrEqual,
            AllowBlank = true,
            ShowErrorMessage = true,
            ErrorStyle = DataValidationErrorStyleValues.Stop,
            SequenceOfReferences = new ListValue<StringValue> { InnerText = string.Join(" ", sqrefParts) },
        };
        validation.Append(new Formula1("0"));
        AppendValidation(validations, validation);
    }

    /// <summary>
    /// A custom-formula validation (revision 6 §2): <paramref name="formula"/> is evaluated per
    /// row, relative to the sqref's own top-left cell -- exactly like a conditional-formatting
    /// formula -- so one canonical formula (written for the sqref's first row) covers every row in
    /// <paramref name="sqrefParts"/>. The error message states the rule itself in three languages,
    /// not merely "invalid value" (§2's own requirement), and <c>allowBlank</c> is left on: this
    /// guards a sum from exceeding the sentence's word count, it does not make an entry mandatory.
    /// This is enforcement Excel's own UI honours; it does not stop a pasted value from bypassing
    /// it (§10.3), which is why <c>ReportBuilder</c> re-checks the same invariant on read-back.
    /// </summary>
    private static void AddCustomValidation(DataValidations validations, IReadOnlyList<string> sqrefParts, string formula, string errorTitle, string errorMessage)
    {
        if (sqrefParts.Count == 0) return;
        var validation = new DataValidation
        {
            Type = DataValidationValues.Custom,
            AllowBlank = true,
            ShowErrorMessage = true,
            ErrorStyle = DataValidationErrorStyleValues.Stop,
            ErrorTitle = errorTitle,
            Error = errorMessage,
            SequenceOfReferences = new ListValue<StringValue> { InnerText = string.Join(" ", sqrefParts) },
        };
        validation.Append(new Formula1(formula));
        AppendValidation(validations, validation);
    }

    /// <summary>
    /// One choke point for every <see cref="DataValidation"/> this builder writes (revision 8 §1):
    /// <c>errorTitle</c>/<c>promptTitle</c> are capped at 32 characters and <c>error</c>/
    /// <c>prompt</c> at 255 by Excel itself, not by the SpreadsheetML schema -- <see
    /// cref="DocumentFormat.OpenXml.Validation.OpenXmlValidator"/> does not catch either limit
    /// (<c>ST_Xstring</c> carries no length restriction), so a file that exceeds one validates
    /// clean and still fails to open ("We found a problem with some content"). Revision 6 found
    /// the 32-character title limit the hard way; revision 8 found the 255-character message limit
    /// the same way, because each validation call checked its own text by eye instead of the
    /// generator enforcing either limit. Throwing here, at generation time, replaces both.
    /// </summary>
    private static void AppendValidation(DataValidations validations, DataValidation validation)
    {
        CheckValidationTextLength(validation.ErrorTitle?.Value, 32, "ErrorTitle");
        CheckValidationTextLength(validation.Error?.Value, 255, "Error");
        CheckValidationTextLength(validation.PromptTitle?.Value, 32, "PromptTitle");
        CheckValidationTextLength(validation.Prompt?.Value, 255, "Prompt");
        validations.Append(validation);
    }

    private static void CheckValidationTextLength(string? text, int limit, string fieldName)
    {
        if (text is not null && text.Length > limit)
        {
            throw new InvalidOperationException(
                $"DataValidation.{fieldName} is {text.Length} characters, exceeding Excel's {limit}-character limit: \"{text}\"");
        }
    }

    /// <summary>
    /// Sheet protection with cell locks enforced but column width and row height left free
    /// (revision 6 §7): <c>formatColumns</c>/<c>formatRows</c> default to locked in SpreadsheetML,
    /// which meant an evaluator reading a 55-character answer column could not widen it or resize a
    /// row to read the whole thing without shrinking the whole window instead.
    /// </summary>
    private static SheetProtection DefaultProtection() => new()
    {
        Sheet = true, Objects = true, Scenarios = true,
        FormatColumns = false, FormatRows = false,
    };

    private static Columns FixedColumns(params (int Min, int Max, double Width)[] widths) =>
        new(widths.Select(w => new Column { Min = (uint)w.Min, Max = (uint)w.Max, Width = w.Width, CustomWidth = true }));

    private static PageMargins DefaultMargins() => new() { Left = 0.5, Right = 0.5, Top = 0.75, Bottom = 0.75, Header = 0.3, Footer = 0.3 };

    private static string ColumnLetter(int oneBasedIndex)
    {
        var dividend = oneBasedIndex;
        var name = "";
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = (char)('A' + modulo) + name;
            dividend = (dividend - modulo - 1) / 26;
        }
        return name;
    }

    /// <summary>
    /// A column header's code line (revision 6 §3): the codes for a whole group joined with " / "
    /// (e.g. "TAM / KISMEN / UYUM_YOK"), so an evaluator can read which code a marking cell takes
    /// without leaving the marking sheet for the Info dictionary.
    /// </summary>
    private static string JoinCodes<TEnum>(IReadOnlyDictionary<TEnum, CodeEntry> map) where TEnum : struct, Enum =>
        string.Join(" / ", map.Values.Select(e => e.Code));

    /// <summary>A four-line column header: the code(s) on their own first line, then EN/TR/RU (revision 6 §3).</summary>
    private static string HeaderWithCode(string codeLine, string en, string tr, string ru) =>
        $"{codeLine}\n{en}\n{tr}\n{ru}";

    private static string TestRoundLabel(int test) =>
        test switch
        {
            1 or 2 => $"Test {test} (round1)",
            3 or 4 => $"Test {test} (round2)",
            _ => $"Test {test}",
        };
}
