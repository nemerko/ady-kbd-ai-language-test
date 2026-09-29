using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Packaging;

namespace AdyKbdTest.Instrument;

public static partial class WorkbookBuilder
{
    // (Min, Max, Width) for A-M, in one place so the header row's height can be computed against
    // the same widths the columns actually get (revision 6 §3: "Başlık satırının yüksekliği yeni
    // içeriğe yetsin"). I and L widened here too (20/30, up from 16/20): once each carries a
    // code line as long as "FARKI_DOGRU_BULDU / FARK_YOK_DEDI / FARKI_YANLIS_BULDU", the old
    // widths wrapped it across several lines for no reason a wider column would not have avoided.
    private static readonly (int Min, int Max, double Width)[] ResponsesColumnWidths =
    [
        (1, 1, 14), (2, 2, 9), (3, 3, 8), (4, 4, 16), (5, 5, 45), (6, 6, 55),
        (7, 7, 9), (8, 8, 9), (9, 9, 20), (10, 10, 22), (11, 11, 14), (12, 12, 30),
        (13, 13, 24),
    ];

    private static void BuildResponsesSheet(WorksheetPart part, WorkbookInput input)
    {
        var sheetData = new SheetData();

        var headerA = "Key\nAnahtar\nКлюч";
        var headerB = "Question\nSoru\nВопрос";
        var headerC = "Tool no\nAraç no\n№ инструмента";
        var headerD = "Tool name\nAraç adı\nИнструмент";
        var headerE = "Question text\nSoru metni\nТекст вопроса";
        var headerF = "Answer text\nYanıt metni\nТекст ответа";
        var headerG = "Word count (tool)\nSözcük sayısı (araç)\nСлов (инструмент)";
        var headerH = "Sentence count (tool)\nCümle sayısı (araç)\nПредложений (инструмент)";
        var headerI = HeaderWithCode(JoinCodes(CodingSchemeText.LanguageCompliance), "Language compliance", "Dil uyumu", "Соответствие языку");
        var headerJ = HeaderWithCode(JoinCodes(CodingSchemeText.SemanticAdequacy), "Semantic adequacy", "Semantik yeterlik", "Семантическая адекватность");
        var headerK = HeaderWithCode(JoinCodes(CodingSchemeText.Marked), "Marked", "Değerlendirildi", "Размечено");
        var headerL = HeaderWithCode(JoinCodes(CodingSchemeText.Question7Verdicts), "Question 7 verdict", "7. soru yargısı", "Оценка вопроса 7");
        var headerM = "Note\nNot\nПримечание";

        var headerHeight = EstimateWrappedRowHeight(
            (headerA, 14), (headerB, 9), (headerC, 8), (headerD, 16), (headerE, 45), (headerF, 55),
            (headerG, 9), (headerH, 9), (headerI, 20), (headerJ, 22), (headerK, 14), (headerL, 30), (headerM, 24));

        AddRow(sheetData, 1u,
        [
            Cell("A", 1u, headerA, StyleHeader),
            Cell("B", 1u, headerB, StyleHeader),
            Cell("C", 1u, headerC, StyleHeader),
            Cell("D", 1u, headerD, StyleHeader),
            Cell("E", 1u, headerE, StyleHeader),
            Cell("F", 1u, headerF, StyleHeader),
            Cell("G", 1u, headerG, StyleHeader),
            Cell("H", 1u, headerH, StyleHeader),
            Cell("I", 1u, headerI, StyleHeader),
            Cell("J", 1u, headerJ, StyleHeader),
            Cell("K", 1u, headerK, StyleHeader),
            Cell("L", 1u, headerL, StyleHeader),
            Cell("M", 1u, headerM, StyleHeader),
        ], height: headerHeight);

        var r = 2u;
        uint? q7FirstRow = null;
        uint? q7LastRow = null;
        foreach (var question in Enumerable.Range(1, 7))
        {
            foreach (var toolFile in input.ResolvedFiles.OrderBy(f => f.Selected.Tool))
            {
                var tool = toolFile.Selected.Tool;
                var id = new ResponseId(input.Test, tool, question);
                if (!input.Analyzed.TryGetValue(id, out var analyzed)) continue;
                var rawQuestion = toolFile.Selected.Questions.First(q => q.Number == question);
                var isQuestion7 = question == 7;

                AddRow(sheetData, r,
                [
                    Cell("A", r, id.ToString(), StyleLockedCenter),
                    Cell("B", r, $"S{question}", StyleLockedCenter),
                    NumberCell("C", r, tool, StyleLockedCenter),
                    Cell("D", r, ToolRegistry.NameOf(tool), StyleLockedCenter),
                    Cell("E", r, rawQuestion.QuestionText, StyleLockedWrap),
                    Cell("F", r, rawQuestion.AnswerText, StyleLockedWrap),
                    NumberCell("G", r, analyzed.Metrics.WordCount, StyleLockedCenter),
                    NumberCell("H", r, analyzed.Metrics.SentenceCount, StyleLockedCenter),
                    EmptyCell("I", r, StyleUnlockedCenter),
                    EmptyCell("J", r, StyleUnlockedCenter),
                    EmptyCell("K", r, StyleUnlockedCenter),
                    // Question 7 verdict is only meaningful -- and only editable -- on question 7's own
                    // rows (§5 item 3); every other question's row keeps this cell locked and blank.
                    EmptyCell("L", r, isQuestion7 ? StyleUnlockedCenter : StyleLockedCenter),
                    EmptyCell("M", r, StyleUnlockedWrap),
                ]);

                if (isQuestion7)
                {
                    q7FirstRow ??= r;
                    q7LastRow = r;
                }

                r++;
            }
        }
        var lastRow = r - 1;

        var dataValidations = new DataValidations();
        if (lastRow >= 2)
        {
            AddListValidation(dataValidations, [$"I2:I{lastRow}"], CodingSchemeText.DropdownValues(CodingSchemeText.LanguageCompliance));
            AddListValidation(dataValidations, [$"J2:J{lastRow}"], CodingSchemeText.DropdownValues(CodingSchemeText.SemanticAdequacy));
            AddListValidation(dataValidations, [$"K2:K{lastRow}"], CodingSchemeText.DropdownValues(CodingSchemeText.Marked));
        }
        if (q7FirstRow is not null)
        {
            AddListValidation(dataValidations, [$"L{q7FirstRow}:L{q7LastRow}"], CodingSchemeText.DropdownValues(CodingSchemeText.Question7Verdicts));
        }
        dataValidations.Count = (uint)dataValidations.ChildElements.Count;

        // Revision 6 §7: freeze only Key + Question (A-B, 23 chars) -- what evaluators actually need
        // pinned while scrolling right is the answer text (F, 55 chars), and freezing all the way
        // through D (47 chars of metadata) pushed it out of frame instead of keeping it in view.
        var sheetView = new SheetView { WorkbookViewId = 0 };
        sheetView.Append(new Pane { HorizontalSplit = 2, VerticalSplit = 1, TopLeftCell = "C2", ActivePane = PaneValues.BottomRight, State = PaneStateValues.Frozen });
        sheetView.Append(new Selection { Pane = PaneValues.BottomRight, ActiveCell = "C2", SequenceOfReferences = new DocumentFormat.OpenXml.ListValue<DocumentFormat.OpenXml.StringValue> { InnerText = "C2" } });

        var columns = FixedColumns(ResponsesColumnWidths);

        var worksheet = new Worksheet(
            new SheetViews(sheetView),
            new SheetFormatProperties { DefaultRowHeight = 15 },
            columns,
            sheetData,
            DefaultProtection(),
            dataValidations,
            DefaultMargins());

        part.Worksheet = worksheet;
    }
}
