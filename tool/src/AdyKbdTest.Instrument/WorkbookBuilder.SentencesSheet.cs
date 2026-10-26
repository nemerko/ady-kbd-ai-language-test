using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Packaging;

namespace AdyKbdTest.Instrument;

public static partial class WorkbookBuilder
{
    /// <summary>
    /// Revision 4 §1: H-K (error counts), M (term choice) and N-R (contact-language counts, four
    /// listed languages plus one "other" total) are all plain non-negative integer entry columns.
    /// L (correct words) and S (total contact-language words) are formulas. T (note) is last.
    /// </summary>
    private static readonly string[] WholeNumberEntryColumns = ["H", "I", "J", "K", "M", "N", "O", "P", "Q", "R"];

    private static readonly (int Min, int Max, double Width)[] SentencesColumnWidths =
    [
        (1, 1, 14), (2, 2, 9), (3, 3, 8), (4, 4, 9), (5, 5, 50), (6, 6, 9),
        (7, 7, 20), (8, 11, 9), (12, 12, 11), (13, 13, 11), (14, 17, 7), (18, 18, 9),
        (19, 19, 14), (20, 20, 20),
    ];

    private static void BuildSentencesSheet(WorksheetPart part, WorkbookInput input)
    {
        var sheetData = new SheetData();

        var headerA = "Key\nAnahtar\nКлюч";
        var headerB = "Question\nSoru\nВопрос";
        var headerC = "Tool no\nAraç no\n№ инструмента";
        var headerD = "Sentence no\nCümle no\n№ предложения";
        var headerE = "Sentence\nCümle\nПредложение";
        var headerF = "Word count (tool)\nSözcük sayısı (araç)\nСлов (инструмент)";
        var headerG = HeaderWithCode(JoinCodes(CodingSchemeText.SentenceVerdicts), "Sentence verdict", "Cümle yargısı", "Оценка предложения");
        var headerH = HeaderWithCode(CodingSchemeText.WordCodes[WordCode.FRM].Code, "Wrong form", "Biçim hatası", "Ошибка формы");
        var headerI = HeaderWithCode(CodingSchemeText.WordCodes[WordCode.CHO].Code, "Wrong word", "Yanlış sözcük", "Неверное слово");
        var headerJ = HeaderWithCode(CodingSchemeText.WordCodes[WordCode.INV].Code, "Invented", "Uydurma", "Выдуманное");
        var headerK = HeaderWithCode(CodingSchemeText.WordCodes[WordCode.MIX].Code, "Code mixing", "Kod karışımı", "Смешение кодов");
        var headerL = "Correct words\nDoğru sözcük\nПравильные слова";
        var headerM = HeaderWithCode(CodingSchemeText.WordCodes[WordCode.TRM].Code, "Term choice", "Terim tercihi", "Выбор термина");
        var headerN = HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.KBD].Code, "Kabardian", "Kabartayca", "Кабардинский");
        var headerO = HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.RUS].Code, "Russian", "Rusça", "Русский");
        var headerP = HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.TUR].Code, "Turkish", "Türkçe", "Турецкий");
        var headerQ = HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.ENG].Code, "English", "İngilizce", "Английский");
        var headerR = HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.OTHER].Code, "Other", "Diğer", "Прочие");
        var headerS = "Total words in other languages\nFarklı dillerdeki sözcük toplamı\nВсего слов на других языках";
        var headerT = "Note\nNot\nПримечание";

        var headerHeight = EstimateWrappedRowHeight(
            (headerA, 14), (headerB, 9), (headerC, 8), (headerD, 9), (headerE, 50), (headerF, 9),
            (headerG, 20), (headerH, 9), (headerI, 9), (headerJ, 9), (headerK, 9), (headerL, 11),
            (headerM, 11), (headerN, 7), (headerO, 7), (headerP, 7), (headerQ, 7), (headerR, 9),
            (headerS, 14), (headerT, 20));

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
            Cell("N", 1u, headerN, StyleHeader),
            Cell("O", 1u, headerO, StyleHeader),
            Cell("P", 1u, headerP, StyleHeader),
            Cell("Q", 1u, headerQ, StyleHeader),
            Cell("R", 1u, headerR, StyleHeader),
            Cell("S", 1u, headerS, StyleHeader),
            Cell("T", 1u, headerT, StyleHeader),
        ], height: headerHeight);

        var r = 2u;
        foreach (var question in Enumerable.Range(1, 7))
        {
            foreach (var toolFile in input.ResolvedFiles.OrderBy(f => f.Selected.Tool))
            {
                var tool = toolFile.Selected.Tool;
                var id = new ResponseId(input.Test, tool, question);
                if (!input.Analyzed.TryGetValue(id, out var analyzed)) continue;

                foreach (var sentence in analyzed.Sentences)
                {
                    var wordCount = sentence.Tokens.Count;
                    AddRow(sheetData, r,
                    [
                        Cell("A", r, id.ToString(), StyleLockedCenter),
                        Cell("B", r, $"S{question}", StyleLockedCenter),
                        NumberCell("C", r, tool, StyleLockedCenter),
                        NumberCell("D", r, sentence.Index + 1, StyleLockedCenter),
                        // Revision 14 §2: the response's original characters, never the repaired counting copy.
                        Cell("E", r, sentence.DisplayText, StyleLockedWrap),
                        NumberCell("F", r, wordCount, StyleLockedCenter),
                        EmptyCell("G", r, StyleUnlockedCenter),
                        EmptyCell("H", r, StyleUnlockedCenter),
                        EmptyCell("I", r, StyleUnlockedCenter),
                        EmptyCell("J", r, StyleUnlockedCenter),
                        EmptyCell("K", r, StyleUnlockedCenter),
                        // Correct words excludes M (term choice) deliberately: TRM is a correctly
                        // formed word, not an error, so it must not reduce the correct-word count
                        // the way revision 3's formula (which subtracted the old L=TRM) mistakenly did.
                        FormulaCell("L", r, $"F{r}-(H{r}+I{r}+J{r}+K{r})", StyleLockedCenter),
                        EmptyCell("M", r, StyleUnlockedCenter),
                        EmptyCell("N", r, StyleUnlockedCenter),
                        EmptyCell("O", r, StyleUnlockedCenter),
                        EmptyCell("P", r, StyleUnlockedCenter),
                        EmptyCell("Q", r, StyleUnlockedCenter),
                        EmptyCell("R", r, StyleUnlockedCenter),
                        FormulaCell("S", r, $"N{r}+O{r}+P{r}+Q{r}+R{r}", StyleLockedCenter),
                        EmptyCell("T", r, StyleUnlockedWrap),
                    ]);
                    r++;
                }
            }
        }
        var lastRow = r - 1;

        var dataValidations = new DataValidations();
        if (lastRow >= 2)
        {
            AddListValidation(dataValidations, [$"G2:G{lastRow}"], CodingSchemeText.DropdownValues(CodingSchemeText.SentenceVerdicts));
            foreach (var col in WholeNumberEntryColumns)
            {
                AddWholeNumberValidation(dataValidations, [$"{col}2:{col}{lastRow}"]);
            }

            // Revision 6 §2: a sentence's error counts (and, separately, term choice) can never add
            // up to more words than the sentence actually has -- each word is counted under exactly
            // one code (§9 of revision 5's Info-sheet rule). Two rules because the two additions
            // are logically separate (errors alone; errors plus term choice), matching §2 exactly.
            // errorTitle is capped at 32 characters by the SpreadsheetML schema (found in revision
            // 6); Error itself has its own, separate 255-character cap that revision 6's full
            // three-language message (394 characters) exceeded -- Excel accepted the file at
            // generation time but refused to open it ("We found a problem with some content"),
            // since OpenXmlValidator does not check this Excel-specific limit either (see
            // AppendValidation, which now enforces both caps centrally so this cannot recur
            // silently). The rule's long-form explanation already lives in the Info sheet's own
            // instructions in all three languages (revision 6 §10.1); this message only needs to
            // state the rule itself, not explain it.
            var oneCodePerWordTitle = "Tek kod / One code / Один код";
            var oneCodePerWordMessage =
                "Her sözcük tek bir koda sayılır; kodların toplamı cümledeki sözcük sayısını geçemez.\n"
                + "One code per word; the codes' total cannot exceed the sentence's word count.\n"
                + "Одно слово — один код; сумма кодов не может превышать число слов.";
            AddCustomValidation(dataValidations, [$"H2:K{lastRow}"], "SUM($H2:$K2)<=$F2", oneCodePerWordTitle, oneCodePerWordMessage);
            AddCustomValidation(dataValidations, [$"M2:M{lastRow}"], "SUM($H2:$K2)+$M2<=$F2", oneCodePerWordTitle, oneCodePerWordMessage);
        }
        dataValidations.Count = (uint)dataValidations.ChildElements.Count;

        var sheetView = new SheetView { WorkbookViewId = 0 };
        sheetView.Append(new Pane { HorizontalSplit = 6, VerticalSplit = 1, TopLeftCell = "G2", ActivePane = PaneValues.BottomRight, State = PaneStateValues.Frozen });
        sheetView.Append(new Selection { Pane = PaneValues.BottomRight, ActiveCell = "G2", SequenceOfReferences = new DocumentFormat.OpenXml.ListValue<DocumentFormat.OpenXml.StringValue> { InnerText = "G2" } });

        var columns = FixedColumns(SentencesColumnWidths);

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
