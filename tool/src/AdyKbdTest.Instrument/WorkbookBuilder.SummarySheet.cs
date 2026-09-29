using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Packaging;

namespace AdyKbdTest.Instrument;

public static partial class WorkbookBuilder
{
    /// <summary>Matches the Summary sheet's B-I column width -- wide enough for a coded, three-language header (revision 6 §6).</summary>
    private const double SummaryDataColumnWidthChars = 16;

    private static void BuildSummarySheet(WorksheetPart part)
    {
        var sheetData = new SheetData();
        var r = 1u;

        void Section(string title)
        {
            var row = r++;
            AddRow(sheetData, row, [Cell("A", row, title, StyleHeader)]);
        }

        // A header row whose cells are (text, columnWidth) pairs -- column A is wider (40) than the
        // data columns (16), so the row's height must be computed from both, like the Info sheet's
        // own header rows (revision 6 §6: "Info sayfasındaki biçimle aynı").
        void HeaderRow(params string[] texts)
        {
            var row = r++;
            var cells = texts.Select((t, i) => (t, i == 0 ? InfoProseColumnWidthChars - 5 : SummaryDataColumnWidthChars)).ToArray();
            var height = EstimateWrappedRowHeight(cells);
            AddRow(sheetData, row, texts.Select((t, i) => Cell(ColumnLetter(i + 1), row, t, StyleHeader)), height: height);
        }

        Section("Summary / Özet / Сводка");
        r++;

        // Per-tool word/error table.
        Section("Errors by tool / Araca göre hatalar / Ошибки по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            "Total words\nToplam sözcük\nВсего слов",
            HeaderWithCode(CodingSchemeText.WordCodes[WordCode.FRM].Code, "Wrong form", "Biçim hatası", "Ошибка формы"),
            HeaderWithCode(CodingSchemeText.WordCodes[WordCode.CHO].Code, "Wrong word", "Yanlış sözcük", "Неверное слово"),
            HeaderWithCode(CodingSchemeText.WordCodes[WordCode.INV].Code, "Invented", "Uydurma", "Выдуманное"),
            HeaderWithCode(CodingSchemeText.WordCodes[WordCode.MIX].Code, "Code mixing", "Kod karışımı", "Смешение кодов"),
            "Total errors\nToplam hata\nВсего ошибок",
            "Errors / 100 words\nHata / 100 sözcük\nОшибок / 100 слов",
            HeaderWithCode(CodingSchemeText.WordCodes[WordCode.TRM].Code, "Term choice (not an error)", "Terim tercihi (hata değil)", "Выбор термина (не ошибка)"));
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            var totalWordsFormula = $"SUMIF(Responses!$C:$C,\"{toolNo}\",Responses!$G:$G)";
            var frmFormula = $"SUMIFS(Sentences!$H:$H,Sentences!$C:$C,\"{toolNo}\")";
            var choFormula = $"SUMIFS(Sentences!$I:$I,Sentences!$C:$C,\"{toolNo}\")";
            var invFormula = $"SUMIFS(Sentences!$J:$J,Sentences!$C:$C,\"{toolNo}\")";
            var mixFormula = $"SUMIFS(Sentences!$K:$K,Sentences!$C:$C,\"{toolNo}\")";
            // Revision 6 §1: TRM moved from column L to M back in revision 4, but this formula still
            // summed L -- which is the *correct words* formula column, not term choice -- so an
            // unmarked workbook reported a large, plausible-looking wrong number here instead of 0.
            var trmFormula = $"SUMIFS(Sentences!$M:$M,Sentences!$C:$C,\"{toolNo}\")";
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, totalWordsFormula, StyleLockedCenter),
                FormulaCell("C", row, frmFormula, StyleLockedCenter),
                FormulaCell("D", row, choFormula, StyleLockedCenter),
                FormulaCell("E", row, invFormula, StyleLockedCenter),
                FormulaCell("F", row, mixFormula, StyleLockedCenter),
                FormulaCell("G", row, $"C{row}+D{row}+E{row}+F{row}", StyleLockedCenter),
                FormulaCell("H", row, $"IF(B{row}=0,0,(C{row}+D{row}+E{row}+F{row})*100/B{row})", StyleLockedCenter),
                FormulaCell("I", row, trmFormula, StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        // Sentence verdict distribution per tool.
        Section("Sentence verdicts by tool / Araca göre cümle yargısı / Оценка предложений по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            HeaderWithCode(CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].Code, "Correct", "Doğru", "Правильно"),
            HeaderWithCode(CodingSchemeText.SentenceVerdicts[SentenceVerdict.YanlisAmaAnlasilir].Code, "Faulty, comprehensible", "Yanlış ama anlaşılır", "Неправильно, но понятно"),
            HeaderWithCode(CodingSchemeText.SentenceVerdicts[SentenceVerdict.YanlisVeAnlamsiz].Code, "Faulty, incomprehensible", "Yanlış ve anlamsız", "Неправильно и непонятно"));
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, $"COUNTIFS(Sentences!$C:$C,\"{toolNo}\",Sentences!$G:$G,\"{CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, $"COUNTIFS(Sentences!$C:$C,\"{toolNo}\",Sentences!$G:$G,\"{CodingSchemeText.SentenceVerdicts[SentenceVerdict.YanlisAmaAnlasilir].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("D", row, $"COUNTIFS(Sentences!$C:$C,\"{toolNo}\",Sentences!$G:$G,\"{CodingSchemeText.SentenceVerdicts[SentenceVerdict.YanlisVeAnlamsiz].DropdownValue}\")", StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        // Language compliance / semantic adequacy distribution (from Responses sheet).
        Section("Language compliance by tool / Araca göre dil uyumu / Соответствие языку по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            HeaderWithCode(CodingSchemeText.LanguageCompliance[LanguageComplianceWord.Tam].Code, "Full", "Tam", "Полное"),
            HeaderWithCode(CodingSchemeText.LanguageCompliance[LanguageComplianceWord.Kismen].Code, "Partial", "Kısmen", "Частичное"),
            HeaderWithCode(CodingSchemeText.LanguageCompliance[LanguageComplianceWord.UyumYok].Code, "None", "Uyum yok", "Нет"));
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$I:$I,\"{CodingSchemeText.LanguageCompliance[LanguageComplianceWord.Tam].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$I:$I,\"{CodingSchemeText.LanguageCompliance[LanguageComplianceWord.Kismen].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("D", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$I:$I,\"{CodingSchemeText.LanguageCompliance[LanguageComplianceWord.UyumYok].DropdownValue}\")", StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        Section("Semantic adequacy by tool / Araca göre semantik yeterlik / Семантическая адекватность по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            HeaderWithCode(CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Karsiladi].Code, "Met", "Karşıladı", "Отвечает"),
            HeaderWithCode(CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.KismenKarsiladi].Code, "Partial", "Kısmen karşıladı", "Частично"),
            HeaderWithCode(CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Alakasiz].Code, "Unrelated", "Alakasız", "Не по теме"));
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$J:$J,\"{CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Karsiladi].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$J:$J,\"{CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.KismenKarsiladi].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("D", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$J:$J,\"{CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Alakasiz].DropdownValue}\")", StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        // Consistency check. Revision 7 §2: this was reading M (term choice) instead of L (correct
        // words) -- a leftover from before revision 4 moved term choice from L to M, flagged in
        // revision 6's notes as a future fix once that revision's own formula freeze lifted. It
        // now also does what it was always meant to: show whether revision 6 §2's own
        // over-marking guard (blocked at entry, but not against a pasted-in value) was bypassed.
        Section("Consistency check / Tutarlılık denetimi / Проверка согласованности");
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row,
                    "Sentences where \"Correct words\" (L) is negative -- should be 0 "
                        + "(shows whether a pasted value bypassed the entry-time guard)\n"
                        + "\"Doğru sözcük\" (L) negatif olan cümleler -- 0 olmalı "
                        + "(yapıştırma yoluyla giriş sırasındaki korumanın aşılıp aşılmadığını gösterir)\n"
                        + "Предложения, где «Правильные слова» (L) отрицательны — должно быть 0 "
                        + "(показывает, была ли обойдена защита при вводе вставкой значения)",
                    StyleLockedWrap),
                FormulaCell("B", row, "COUNTIF(Sentences!$L:$L,\"<0\")", StyleLockedCenter),
            ]);
        }
        r++;

        // Revision 6 §9.1: Question 7 verdict by tool -- the copying probe's human-judgement leg,
        // already exported as test{n}-q7-verdict.tsv but not previously visible in the workbook.
        Section("Question 7 verdict by tool / Araca göre 7. soru yargısı / Оценка вопроса 7 по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            HeaderWithCode(CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_DOGRU_BULDU].Code,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_DOGRU_BULDU].En,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_DOGRU_BULDU].Tr,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_DOGRU_BULDU].Ru),
            HeaderWithCode(CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].Code,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].En,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].Tr,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].Ru),
            HeaderWithCode(CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_YANLIS_BULDU].Code,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_YANLIS_BULDU].En,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_YANLIS_BULDU].Tr,
                CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_YANLIS_BULDU].Ru));
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$L:$L,\"{CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_DOGRU_BULDU].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$L:$L,\"{CodingSchemeText.Question7Verdicts[Question7Verdict.FARK_YOK_DEDI].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("D", row, $"COUNTIFS(Responses!$C:$C,\"{toolNo}\",Responses!$L:$L,\"{CodingSchemeText.Question7Verdicts[Question7Verdict.FARKI_YANLIS_BULDU].DropdownValue}\")", StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        // Revision 6 §9.2: contact languages by tool -- possible only since revision 4 turned these
        // columns into plain integers; column G's total is read straight from Sentences!$S:$S rather
        // than re-summed, since S already is N+O+P+Q+R for each sentence.
        Section("Contact languages by tool / Araca göre temas dilleri / Языки контакта по инструментам");
        HeaderRow(
            "Tool\nAraç\nИнструмент",
            HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.KBD].Code, "Kabardian", "Kabartayca", "Кабардинский"),
            HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.RUS].Code, "Russian", "Rusça", "Русский"),
            HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.TUR].Code, "Turkish", "Türkçe", "Турецкий"),
            HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.ENG].Code, "English", "İngilizce", "Английский"),
            HeaderWithCode(CodingSchemeText.ContactLanguages[ContactLanguage.OTHER].Code, "Other", "Diğer", "Прочие"),
            "Total\nToplam\nВсего");
        foreach (var tool in ToolRegistry.Tools)
        {
            var row = r;
            var toolNo = tool.Number.ToString(CultureInfo.InvariantCulture);
            AddRow(sheetData, row,
            [
                Cell("A", row, toolNo, StyleLockedCenter),
                FormulaCell("B", row, $"SUMIFS(Sentences!$N:$N,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
                FormulaCell("C", row, $"SUMIFS(Sentences!$O:$O,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
                FormulaCell("D", row, $"SUMIFS(Sentences!$P:$P,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
                FormulaCell("E", row, $"SUMIFS(Sentences!$Q:$Q,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
                FormulaCell("F", row, $"SUMIFS(Sentences!$R:$R,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
                FormulaCell("G", row, $"SUMIFS(Sentences!$S:$S,Sentences!$C:$C,\"{toolNo}\")", StyleLockedCenter),
            ]);
            r++;
        }
        r++;

        // Revision 6 §9.3: marking progress -- so "where did we get to" has an answer inside the
        // workbook itself, without checking generated TSVs, across what will be days of marking.
        Section("Marking progress / İşaretleme ilerlemesi / Прогресс разметки");
        HeaderRow(
            "Metric\nÖlçüt\nПоказатель",
            "Marked\nİşaretlenen\nРазмечено",
            "Total\nToplam\nВсего");
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "Marked responses\nİşaretlenmiş yanıtlar\nРазмеченные ответы", StyleLockedWrap),
                FormulaCell("B", row, $"COUNTIF(Responses!$K:$K,\"{CodingSchemeText.Marked[MarkedWord.Evet].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, "COUNTA(Responses!$A:$A)-1", StyleLockedCenter),
            ]);
        }
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "Sentences with a verdict\nYargısı girilmiş cümleler\nПредложения с оценкой", StyleLockedWrap),
                FormulaCell("B", row, "COUNTA(Sentences!$G:$G)-1", StyleLockedCenter),
                FormulaCell("C", row, "COUNTA(Sentences!$A:$A)-1", StyleLockedCenter),
            ]);
        }
        {
            // Revision 7 §3: kept from the "Marked responses" block this pass removes as a
            // duplicate of the two rows above -- the "hayır / нет" count still has no other home.
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "Unmarked responses\nİşaretlenmemiş yanıtlar\nНеразмеченные ответы", StyleLockedWrap),
                FormulaCell("B", row, $"COUNTIF(Responses!$K:$K,\"{CodingSchemeText.Marked[MarkedWord.Hayir].DropdownValue}\")", StyleLockedCenter),
                FormulaCell("C", row, "COUNTA(Responses!$A:$A)-1", StyleLockedCenter),
            ]);
        }

        var worksheet = new Worksheet(
            new SheetFormatProperties { DefaultRowHeight = 15 },
            FixedColumns((1, 1, 40), (2, 9, SummaryDataColumnWidthChars)),
            sheetData,
            DefaultProtection(),
            DefaultMargins());

        part.Worksheet = worksheet;
    }
}
