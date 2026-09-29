using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Packaging;

namespace AdyKbdTest.Instrument;

public static partial class WorkbookBuilder
{
    /// <summary>Matches the Info sheet's B/C/D column width (revision 5 §3's suggested layout).</summary>
    private const double InfoProseColumnWidthChars = 45;

    /// <summary>
    /// The word codes' Code-column text, spelled out as "CODE: root / root / root" (revision 6
    /// §8.2). One source for both places that show it -- the code dictionary and, since revision
    /// 10 §1, the decision table -- so the two cannot drift apart the way a hand-typed second copy
    /// eventually would.
    /// </summary>
    private static readonly IReadOnlyDictionary<WordCode, string> WordCodeRootExpansions = new Dictionary<WordCode, string>
    {
        [WordCode.FRM] = "FRM: form / biçim / форма",
        [WordCode.CHO] = "CHO: choice / seçim / выбор",
        [WordCode.MIX] = "MIX: mixing / karışma / смешение",
        [WordCode.INV] = "INV: invented / uydurma / выдуманное",
        [WordCode.TRM] = "TRM: term / terim / термин",
    };

    /// <summary>
    /// Revision 5 rewrites this sheet's layout entirely (§1): every previous helper wrote all
    /// three languages into column A, stacked in one cell, which meant B-D sat empty while A tried
    /// to hold everything at a 22-character width -- rows grew very tall and still clipped the
    /// Turkish/Russian portions off-screen. The rule now is one rule for the whole sheet: A carries
    /// only short content (a code, or a stacked-but-short label); every long or per-language piece
    /// of text gets its own cell in B (EN), C (TR) or D (RU).
    /// </summary>
    private static void BuildInfoSheet(WorksheetPart part, WorkbookInput input)
    {
        var sheetData = new SheetData();
        var r = 1u;

        void Header(string en, string tr, string ru)
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "", StyleHeader),
                Cell("B", row, en, StyleHeader),
                Cell("C", row, tr, StyleHeader),
                Cell("D", row, ru, StyleHeader),
            ]);
        }

        void SubHeader(string en, string tr, string ru)
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "", StyleSubHeader),
                Cell("B", row, en, StyleSubHeader),
                Cell("C", row, tr, StyleSubHeader),
                Cell("D", row, ru, StyleSubHeader),
            ]);
        }

        // Metadata rows: a short, three-language label stacked in A (never more than a few words
        // per language, so three lines is always enough) and one value in B. Only "Evaluation
        // method" needs its value in all three languages (§4); its caller stacks that value the
        // same way the label is stacked, rather than this helper growing a rarely-used third mode.
        // Revision 6 §5: height is computed from both cells, like Prose's -- a fixed 48pt clipped
        // the Russian line of a three-language value ("Evaluation method") and would clip "Source
        // files" too once enough tool files pushed its value past one wrapped line.
        void Plain(string enLabel, string trLabel, string ruLabel, string value)
        {
            var row = r++;
            var label = $"{enLabel}\n{trLabel}\n{ruLabel}";
            var height = EstimateWrappedRowHeight((label, 28), (value, InfoProseColumnWidthChars));
            AddRow(sheetData, row,
            [
                Cell("A", row, label, StyleLockedWrap),
                Cell("B", row, value, StyleLockedWrap),
            ], height: height);
        }

        void FourCol(string code, string en, string tr, string ru)
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, code, StyleLockedCenter),
                Cell("B", row, en, StyleLockedWrap),
                Cell("C", row, tr, StyleLockedWrap),
                Cell("D", row, ru, StyleLockedWrap),
            ]);
        }

        // Same four columns as FourCol, but with a computed row height (revision 10 §1/§5): the
        // decision table's questions are full sentences now, long enough to wrap at the B/C/D
        // columns' 45-character width, where every other FourCol call's content (a short code, a
        // one-line category definition) never needed more than the sheet's default row height.
        void FourColTall(string code, string en, string tr, string ru)
        {
            var row = r++;
            var height = EstimateWrappedRowHeight((code, 40), (en, InfoProseColumnWidthChars), (tr, InfoProseColumnWidthChars), (ru, InfoProseColumnWidthChars));
            AddRow(sheetData, row,
            [
                Cell("A", row, code, StyleLockedCenter),
                Cell("B", row, en, StyleLockedWrap),
                Cell("C", row, tr, StyleLockedWrap),
                Cell("D", row, ru, StyleLockedWrap),
            ], height: height);
        }

        // A long, per-language block of prose: each language gets its own cell (B/C/D), and the
        // row's height is set explicitly from the longest of the three once wrapped at the column's
        // own width (§3) -- the default single-line row height was what clipped this content before.
        void Prose(string en, string tr, string ru)
        {
            var row = r++;
            var height = EstimateWrappedRowHeight(InfoProseColumnWidthChars, en, tr, ru);
            AddRow(sheetData, row,
            [
                Cell("B", row, en, StyleLockedWrap),
                Cell("C", row, tr, StyleLockedWrap),
                Cell("D", row, ru, StyleLockedWrap),
            ], height: height);
        }

        // Title -- written once, directly, not through Header() (§2: Header() now spreads its three
        // languages into B-D, which the product name does not need; it has no translation).
        {
            var row = r++;
            AddRow(sheetData, row, [Cell("A", row, AppInfo.DisplayName, StyleHeader)]);
        }
        r++;

        // Framing paragraph -- what this test measures. Load-bearing text (§4.1 of the base
        // request), given verbatim in Turkish by the task document; the English and Russian
        // versions are this tool's own translation of it.
        Header("What this test measures", "Bu test neyi ölçüyor", "Что измеряет этот тест");
        Prose(
            "This test does not measure translation quality, but Adyghe conversational competence. "
                + "Every question is a request expressed in Adyghe. What is judged is whether the tool "
                + "understood this request and attempted to meet it. Questions four and five contain a "
                + "translation task, but what is measured is not the accuracy of the translation, but "
                + "whether the request was understood: even a wrong translation is still a translation, "
                + "and shows that the request was understood. Returning a Turkish answer to a Turkish "
                + "paragraph would mean the request was not understood.",
            "Bu test çeviri kalitesini değil, Adıgece sohbet yetkinliğini ölçer. Her soru Adıgece "
                + "ifade edilmiş bir istektir. Yargılanan şey, aracın bu isteği anlayıp anlamadığı ve "
                + "karşılamaya çalışıp çalışmadığıdır. Dördüncü ve beşinci sorularda bir çeviri görevi "
                + "vardır, ama ölçülen çevirinin doğruluğu değil, isteğin anlaşılmasıdır: yanlış çeviri de "
                + "bir çeviridir ve isteğin anlaşıldığını gösterir. Türkçe bir paragraf verildiğinde Türkçe "
                + "yanıt dönmesi, isteğin anlaşılmadığı anlamına gelirdi.",
            "Этот тест измеряет не качество перевода, а разговорную компетентность на адыгейском языке. "
                + "Каждый вопрос — это просьба, выраженная на адыгейском. "
                + "Оценивается то, понял ли инструмент эту просьбу и попытался ли её выполнить. "
                + "В четвёртом и пятом вопросах есть задача перевода, но измеряется не точность перевода, а понимание просьбы: "
                + "даже неверный перевод остаётся переводом и показывает, что просьба была понята. "
                + "Если на турецкий абзац был бы дан турецкий ответ, это означало бы, что просьба не была понята.");
        r++;

        // Metadata
        Header("Metadata", "Künye", "Метаданные");
        Plain("Test / Round", "Test / Tur", "Тест / Раунд", TestRoundLabel(input.Test));
        Plain("Generated (UTC)", "Üretildi (UTC)", "Создано (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Plain("Source files", "Kaynak dosyalar", "Исходные файлы", string.Join(", ", input.ResolvedFiles.Select(f => f.Selected.SourceFileName)));
        Plain("Tool version", "Araç sürümü", "Версия инструмента", $"{AppInfo.DisplayName} {AppInfo.Version}");
        Plain("Evaluators", "Değerlendiriciler", "Оценщики", "Mehmet Uğur Nemlioğlu, Dr. Saida Abregova Nemlioğlu");
        Plain("Evaluation method", "Değerlendirme yöntemi", "Метод оценки",
            "Consensus: both evaluators read and discuss together, and record one agreed judgement\n"
            + "Uzlaşı: ikisi birlikte okur, tartışır, tek hüküm kaydeder\n"
            + "\u041A\u043E\u043D\u0441\u0435\u043D\u0441\u0443\u0441: \u043E\u0431\u0430 \u043E\u0446\u0435\u043D\u0449\u0438\u043A\u0430 \u0447\u0438\u0442\u0430\u044E\u0442 \u0438 \u043E\u0431\u0441\u0443\u0436\u0434\u0430\u044E\u0442 \u0432\u043C\u0435\u0441\u0442\u0435 \u0438 \u0437\u0430\u043F\u0438\u0441\u044B\u0432\u0430\u044E\u0442 \u043E\u0434\u043D\u043E \u0441\u043E\u0433\u043B\u0430\u0441\u043E\u0432\u0430\u043D\u043D\u043E\u0435 \u0440\u0435\u0448\u0435\u043D\u0438\u0435");
        {
            var row = r++;
            AddRow(sheetData, row,
            [
                Cell("A", row, "Evaluation date (fill in)\nDeğerlendirme tarihi (doldurun)\n\u0414\u0430\u0442\u0430 \u043E\u0446\u0435\u043D\u043A\u0438 (\u0437\u0430\u043F\u043E\u043B\u043D\u0438\u0442\u0435)", StyleLockedWrap),
                Cell("B", row, "", StyleUnlockedWrap),
            ], height: 75);
        }
        Plain("Total responses", "Toplam yanıt", "Всего ответов", (input.ResolvedFiles.Count * 7).ToString(CultureInfo.InvariantCulture));
        r++;

        // Decision table -- with the code each answer resolves to (revision 3 §2). Revision 6 §4
        // reorders this to FRM, CHO, MIX, INV, TRM (MIX now comes before INV -- see below).
        // Revision 10 rewrote all five questions and gave the Code column the same "CODE: root /
        // root / root" expansion the code dictionary already used (§1), from the one shared source
        // above -- and removed INV's second sentence ("does such a word not exist in any
        // language?"), which the table's own row order (MIX already checked first) had made both
        // redundant and unanswerable: an evaluator cannot verify a word does not exist in any
        // language, only that Adyghe's own dictionary has no adapted, source-language, or invented
        // form of it. The Turkish, English and Russian text below is final as given -- it is not a
        // translation of the others; each was independently reviewed, the Russian by the study's
        // Russian-reading evaluator, and none may be reworded, "corrected", or re-translated.
        Header("Decision table", "Karar tablosu", "Таблица решений");
        FourCol("Code", "Question", "Soru", "Вопрос");
        FourColTall(WordCodeRootExpansions[WordCode.FRM],
            "Is the right word chosen, but its affix or inflection wrong?",
            "Doğru sözcük seçilmiş ama eki ya da çekimi mi yanlış?",
            "Слово выбрано правильно, но неверен его аффикс или словоизменение?");
        FourColTall(WordCodeRootExpansions[WordCode.CHO],
            "Is the word well formed, but the wrong word for the intended meaning?",
            "Sözcük doğru kurulmuş ama anlam için yanlış sözcük mü seçilmiş?",
            "Слово образовано правильно, но по смыслу выбрано не то слово?");
        FourColTall(WordCodeRootExpansions[WordCode.MIX],
            "Is the word taken from another language and left in that language's own form? The contact language is recorded as well.",
            "Sözcük başka bir dilden, o dilin kendi biçimiyle olduğu gibi mi alınmış? Kaynak dil ayrıca kaydedilir.",
            "Слово взято из другого языка и оставлено в его собственной форме? Язык-источник записывается отдельно.");
        FourColTall(WordCodeRootExpansions[WordCode.INV],
            "Even if the root and affixes are familiar, is this a combination the language does not use?",
            "Kök ve ekler tanıdık olsa bile, bu birleşimi dil kullanmıyor mu?",
            "Даже если корень и аффиксы знакомы, такое сочетание в языке не используется?");
        FourColTall(WordCodeRootExpansions[WordCode.TRM],
            "Is the word an established loan, adapted to the language, used where a recognised native term exists? This is not an error.",
            "Sözcük, dile uyarlanmış yerleşik bir alıntı ama kabul görmüş yerli bir karşılığı var mı? Hata değildir.",
            "Слово — устоявшееся заимствование, приспособленное к языку, но есть признанный местный термин? Это не ошибка.");
        // §9: without this, the same word can silently end up counted under two codes at once,
        // and the correct-words formula and the contact-language-total check both break with no
        // visible cause.
        Prose(
            "Each word is counted under exactly one code. Read the table top to bottom; the code of "
                + "the first row that answers \"yes\" is that word's code. Counting a word under two "
                + "categories at once corrupts both the correct-word count and the contact-language total.",
            "Her sözcük yalnızca bir koda sayılır. Tabloyu yukarıdan aşağıya okuyun; ilk \"evet\" "
                + "yanıtını veren satırın kodu o sözcüğün kodudur. Bir sözcüğü iki kategoriye birden "
                + "saymak, doğru sözcük sayısını ve temas dili toplamını bozar.",
            "\u041A\u0430\u0436\u0434\u043E\u0435 \u0441\u043B\u043E\u0432\u043E \u0437\u0430\u0441\u0447\u0438\u0442\u044B\u0432\u0430\u0435\u0442\u0441\u044F \u0442\u043E\u043B\u044C\u043A\u043E \u043F\u043E \u043E\u0434\u043D\u043E\u043C\u0443 \u043A\u043E\u0434\u0443. \u0427\u0438\u0442\u0430\u0439\u0442\u0435 \u0442\u0430\u0431\u043B\u0438\u0446\u0443 \u0441\u0432\u0435\u0440\u0445\u0443 \u0432\u043D\u0438\u0437; "
                + "\u043A\u043E\u0434 \u0442\u043E\u0439 \u0441\u0442\u0440\u043E\u043A\u0438, \u043D\u0430 \u043A\u043E\u0442\u043E\u0440\u0443\u044E \u043F\u0435\u0440\u0432\u044B\u043C \u043F\u043E\u043B\u0443\u0447\u0435\u043D \u043E\u0442\u0432\u0435\u0442 \u00AB\u0434\u0430\u00BB, \u0438 \u0435\u0441\u0442\u044C \u043A\u043E\u0434 \u044D\u0442\u043E\u0433\u043E \u0441\u043B\u043E\u0432\u0430. "
                + "\u0415\u0441\u043B\u0438 \u0437\u0430\u0441\u0447\u0438\u0442\u0430\u0442\u044C \u0441\u043B\u043E\u0432\u043E \u0441\u0440\u0430\u0437\u0443 \u0432 \u0434\u0432\u0435 \u043A\u0430\u0442\u0435\u0433\u043E\u0440\u0438\u0438, \u044D\u0442\u043E \u0438\u0441\u043F\u043E\u0440\u0442\u0438\u0442 \u0438 \u043F\u043E\u0434\u0441\u0447\u0451\u0442 \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u044B\u0445 \u0441\u043B\u043E\u0432, "
                + "\u0438 \u0441\u0443\u043C\u043C\u0443 \u044F\u0437\u044B\u043A\u043E\u0432 \u043A\u043E\u043D\u0442\u0430\u043A\u0430\u0442\u0430.");
        // Revision 10, item 3: the question rows alone were not carrying these two boundaries in
        // practice -- this is exactly where marking got stuck. The second paragraph already exists
        // in protocol/coding-scheme.md; it belongs here too, since evaluators work from the
        // workbook, not the protocol document. Final text as given, not a translation of the others.
        Prose(
            "A practical way to tell these three apart: try writing the word correctly. If you can "
                + "write a proper form of the same word, it is FRM; if you can, but what you write is "
                + "a different word, it is CHO; if there is no correct form to write, it is INV.",
            "Bu \u00FC\u00E7\u00FCn\u00FC ay\u0131rman\u0131n pratik yolu: s\u00F6zc\u00FC\u011F\u00FCn do\u011Frusunu yazmay\u0131 dene. Ayn\u0131 s\u00F6zc\u00FC\u011F\u00FCn d\u00FCzg\u00FCn "
                + "h\u00E2lini yazabiliyorsan FRM; yazabiliyorsun ama yazd\u0131\u011F\u0131n ba\u015Fka bir s\u00F6zc\u00FCkse CHO; "
                + "yazacak do\u011Fru bir h\u00E2l yoksa INV.",
            "\u041F\u0440\u0430\u043A\u0442\u0438\u0447\u0435\u0441\u043A\u0438\u0439 \u0441\u043F\u043E\u0441\u043E\u0431 \u0440\u0430\u0437\u043B\u0438\u0447\u0438\u0442\u044C \u044D\u0442\u0438 \u0442\u0440\u0438: \u043F\u043E\u043F\u0440\u043E\u0431\u0443\u0439\u0442\u0435 \u043D\u0430\u043F\u0438\u0441\u0430\u0442\u044C \u0441\u043B\u043E\u0432\u043E \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u043E. \u0415\u0441\u043B\u0438 \u0432\u044B "
                + "\u043C\u043E\u0436\u0435\u0442\u0435 \u043D\u0430\u043F\u0438\u0441\u0430\u0442\u044C \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u0443\u044E \u0444\u043E\u0440\u043C\u0443 \u0442\u043E\u0433\u043E \u0436\u0435 \u0441\u043B\u043E\u0432\u0430 \u2014 \u044D\u0442\u043E FRM; \u0435\u0441\u043B\u0438 \u043C\u043E\u0436\u0435\u0442\u0435, \u043D\u043E "
                + "\u043D\u0430\u043F\u0438\u0441\u0430\u043D\u043D\u043E\u0435 \u2014 \u0434\u0440\u0443\u0433\u043E\u0435 \u0441\u043B\u043E\u0432\u043E, \u044D\u0442\u043E CHO; \u0435\u0441\u043B\u0438 \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u043E\u0439 \u0444\u043E\u0440\u043C\u044B \u043D\u0435 \u0441\u0443\u0449\u0435\u0441\u0442\u0432\u0443\u0435\u0442 \u2014 \u044D\u0442\u043E INV.");
        Prose(
            "The boundary between MIX and TRM is adaptation. A word left in the source language's "
                + "form is MIX; a word adapted to this language's morphology and part of its lexicon "
                + "is TRM -- and TRM is not an error.",
            "MIX ile TRM aras\u0131ndaki ayr\u0131m uyarlamad\u0131r. S\u00F6zc\u00FCk kaynak dilin bi\u00E7iminde duruyorsa MIX; "
                + "dilin bi\u00E7imbilgisine uyarlanm\u0131\u015F ve s\u00F6z varl\u0131\u011F\u0131na girmi\u015Fse TRM \u2014 ve TRM hata de\u011Fildir.",
            "\u0413\u0440\u0430\u043D\u0438\u0446\u0430 \u043C\u0435\u0436\u0434\u0443 MIX \u0438 TRM \u2014 \u044D\u0442\u043E \u0430\u0434\u0430\u043F\u0442\u0430\u0446\u0438\u044F. \u0421\u043B\u043E\u0432\u043E, \u043E\u0441\u0442\u0430\u0432\u043B\u0435\u043D\u043D\u043E\u0435 \u0432 \u0444\u043E\u0440\u043C\u0435 \u044F\u0437\u044B\u043A\u0430-\u0438\u0441\u0442\u043E\u0447\u043D\u0438\u043A\u0430, "
                + "\u2014 \u044D\u0442\u043E MIX; \u0441\u043B\u043E\u0432\u043E, \u043F\u0440\u0438\u0441\u043F\u043E\u0441\u043E\u0431\u043B\u0435\u043D\u043D\u043E\u0435 \u043A \u043C\u043E\u0440\u0444\u043E\u043B\u043E\u0433\u0438\u0438 \u044D\u0442\u043E\u0433\u043E \u044F\u0437\u044B\u043A\u0430 \u0438 \u0432\u043E\u0448\u0435\u0434\u0448\u0435\u0435 \u0432 \u0435\u0433\u043E "
                + "\u0441\u043B\u043E\u0432\u0430\u0440\u043D\u044B\u0439 \u0441\u043E\u0441\u0442\u0430\u0432, \u2014 \u044D\u0442\u043E TRM, \u0438 TRM \u043D\u0435 \u044F\u0432\u043B\u044F\u0435\u0442\u0441\u044F \u043E\u0448\u0438\u0431\u043A\u043E\u0439.");
        r++;

        // Code dictionary -- four columns (Code/EN/TR/RU), grouped under a sheet+column sub-header
        // per group (revision 3 §1): the code is the same string an evaluator sees stored nowhere
        // in a dropdown cell (dropdowns store the TR/RU wording, §4 of revision 1) but that reports,
        // summaries and the Sentences/Responses sheet headers refer back to -- without the code
        // column, there was no way to connect the two.
        Header("Code dictionary", "Kod sözlüğü", "Словарь кодов");
        FourCol("Code", "EN", "TR", "RU");

        SubHeader("Sentence verdict (Sentences sheet, column G)", "Cümle yargısı (Cümleler sayfası, G sütunu)", "Оценка предложения (лист Предложения, столбец G)");
        foreach (var e in CodingSchemeText.SentenceVerdicts.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);

        SubHeader("Word codes (Sentences sheet: error codes H-K, term choice M)", "Sözcük kodları (Cümleler sayfası: hata kodları H–K, terim tercihi M)", "Коды слов (лист Предложения: коды ошибок H-K, выбор термина M)");
        // Revision 6 §8.2: unlike every other group, this Code cell spells out what the abbreviation
        // is short for (its English root, given in all three languages) rather than just the bare
        // code -- the category's own definition still lives in the adjacent EN/TR/RU columns.
        FourCol(WordCodeRootExpansions[WordCode.FRM], CodingSchemeText.WordCodes[WordCode.FRM].En, CodingSchemeText.WordCodes[WordCode.FRM].Tr, CodingSchemeText.WordCodes[WordCode.FRM].Ru);
        FourCol(WordCodeRootExpansions[WordCode.CHO], CodingSchemeText.WordCodes[WordCode.CHO].En, CodingSchemeText.WordCodes[WordCode.CHO].Tr, CodingSchemeText.WordCodes[WordCode.CHO].Ru);
        FourCol(WordCodeRootExpansions[WordCode.INV], CodingSchemeText.WordCodes[WordCode.INV].En, CodingSchemeText.WordCodes[WordCode.INV].Tr, CodingSchemeText.WordCodes[WordCode.INV].Ru);
        FourCol(WordCodeRootExpansions[WordCode.MIX], CodingSchemeText.WordCodes[WordCode.MIX].En, CodingSchemeText.WordCodes[WordCode.MIX].Tr, CodingSchemeText.WordCodes[WordCode.MIX].Ru);
        FourCol(WordCodeRootExpansions[WordCode.TRM], CodingSchemeText.WordCodes[WordCode.TRM].En, CodingSchemeText.WordCodes[WordCode.TRM].Tr, CodingSchemeText.WordCodes[WordCode.TRM].Ru);

        SubHeader("Language compliance (Responses sheet, column I)", "Dil uyumu (Yanıtlar sayfası, I sütunu)", "Соответствие языку (лист Ответы, столбец I)");
        foreach (var e in CodingSchemeText.LanguageCompliance.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);

        SubHeader("Semantic adequacy (Responses sheet, column J)", "Semantik yeterlik (Yanıtlar sayfası, J sütunu)", "Семантическая адекватность (лист Ответы, столбец J)");
        foreach (var e in CodingSchemeText.SemanticAdequacy.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);

        SubHeader("Marked (Responses sheet, column K)", "İşaretlendi (Yanıtlar sayfası, K sütunu)", "Размечено (лист Ответы, столбец K)");
        foreach (var e in CodingSchemeText.Marked.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);

        SubHeader("Question 7 verdict (Responses sheet, column L)", "7. soru yargısı (Yanıtlar sayfası, L sütunu)", "Оценка вопроса 7 (лист Ответы, столбец L)");
        foreach (var e in CodingSchemeText.Question7Verdicts.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);

        // §6: this group's entries, "Other" included, now come entirely from CodingSchemeText.
        // ContactLanguages -- previously "Other" was a one-off hand-written FourCol call here whose
        // Russian text ("Другой") disagreed with the Sentences!R column header an evaluator actually
        // sees ("Прочие"); keeping one name in one dictionary means that cannot happen again.
        SubHeader("Contact languages (Sentences sheet, columns N-R)", "Temas dilleri (Cümleler sayfası, N–R sütunları)", "Языки контакта (лист Предложения, столбцы N-R)");
        foreach (var e in CodingSchemeText.ContactLanguages.Values) FourCol(e.Code, e.En, e.Tr, e.Ru);
        r++;

        // TRM explanatory block with the three-way boundary example (revision 3 §3), the
        // correct-words note (revision 4 §6) and the contact-language note (revision 5 §10).
        Header("What TRM means", "TRM ne demek", "Что означает TRM");
        {
            var en = "TRM is not a language error. The word is correctly formed and the meaning is right; "
                + "only an established loan term was chosen when a recognised native equivalent exists. "
                + "What it measures is not the tool's competence, but whether term planning has reached "
                + "that tool. This is why it is counted separately from the error columns.\n\n"
                + "The boundary is adaptation:\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044D \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- adapted to Adyghe morphology, part of the "
                + "vocabulary -> TRM if used while a native equivalent (\u04C0\u044D\u0440\u044B\u0448\u04CF \u0430\u043A\u044A\u044B\u043B) exists\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044B\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- in the source language's own form -> MIX (contact language: RUS)\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u0430\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- standard in neither Russian nor Adyghe -> INV\n\n"
                + "Term choice does not reduce the correct-word count -- that is why it sits apart "
                + "from the error columns, after the correct-words column, instead of among them.\n\n"
                + "Words counted as term choice are never written into the contact-language columns. "
                + "Those columns are only the source-language breakdown of words counted as code "
                + "mixing (MIX); that is why their total must equal the code-mixing column.";
            var tr = "TRM bir dil hatası değildir. Sözcük doğru kurulmuştur, anlam yerindedir; yalnızca kabul "
                + "görmüş yerli karşılık varken yerleşik ödünç terim seçilmiştir. Ölçtüğü şey aracın "
                + "yetkinliği değil, terim planlamasının o araca ulaşıp ulaşmadığıdır. Bu yüzden hata "
                + "sütunlarından ayrı sayılır.\n\n"
                + "Sınır uyarlanmadır:\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044D \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- Adıge biçimbilimine uyarlanmış, sözvarlığının "
                + "parçası -> yerli karşılık (\u04C0\u044D\u0440\u044B\u0448\u04CF \u0430\u043A\u044A\u044B\u043B) varken kullanılmışsa TRM\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044B\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- kaynak dilin kendi biçiminde -> MIX (temas dili: RUS)\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u0430\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 -- ne Rusçada ne Adıgecede ölçünlü -> INV\n\n"
                + "Terim tercihi doğru sözcük sayısını azaltmaz -- bu yüzden hata sütunlarının "
                + "arasında değil, doğru sözcük sütununun ardında, onlardan ayrı durur.\n\n"
                + "Terim tercihi sayılan sözcükler temas dili sütunlarına yazılmaz. Temas dili "
                + "sütunları yalnızca kod karışımı (MIX) sayılan sözcüklerin kaynak dile göre "
                + "dağılımıdır; bu yüzden toplamları kod karışımı sütununa eşit olmalıdır.";
            var ru = "TRM \u2014 \u044D\u0442\u043E \u043D\u0435 \u044F\u0437\u044B\u043A\u043E\u0432\u0430\u044F \u043E\u0448\u0438\u0431\u043A\u0430. \u0421\u043B\u043E\u0432\u043E \u043E\u0431\u0440\u0430\u0437\u043E\u0432\u0430\u043D\u043E \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u043E, \u0437\u043D\u0430\u0447\u0435\u043D\u0438\u0435 \u0443\u043C\u0435\u0441\u0442\u043D\u043E; "
                + "\u0432\u044B\u0431\u0440\u0430\u043D \u043B\u0438\u0448\u044C \u0443\u0441\u0442\u043E\u044F\u0432\u0448\u0438\u0439\u0441\u044F \u0437\u0430\u0438\u043C\u0441\u0442\u0432\u043E\u0432\u0430\u043D\u043D\u044B\u0439 \u0442\u0435\u0440\u043C\u0438\u043D \u043F\u0440\u0438 \u043D\u0430\u043B\u0438\u0447\u0438\u0438 \u043F\u0440\u0438\u0437\u043D\u0430\u043D\u043D\u043E\u0433\u043E \u043C\u0435\u0441\u0442\u043D\u043E\u0433\u043E \u044D\u043A\u0432\u0438\u0432\u0430\u043B\u0435\u043D\u0442\u0430. "
                + "\u0418\u0437\u043C\u0435\u0440\u044F\u0435\u0442\u0441\u044F \u043D\u0435 \u043A\u043E\u043C\u043F\u0435\u0442\u0435\u043D\u0442\u043D\u043E\u0441\u0442\u044C \u0438\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u0430, \u0430 \u0442\u043E, \u0434\u043E\u0448\u043B\u043E \u043B\u0438 \u0442\u0435\u0440\u043C\u0438\u043D\u043E\u043B\u043E\u0433\u0438\u0447\u0435\u0441\u043A\u043E\u0435 \u043F\u043B\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435 \u0434\u043E \u044D\u0442\u043E\u0433\u043E \u0438\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u0430. "
                + "\u041F\u043E\u044D\u0442\u043E\u043C\u0443 \u043E\u043D \u0441\u0447\u0438\u0442\u0430\u0435\u0442\u0441\u044F \u043E\u0442\u0434\u0435\u043B\u044C\u043D\u043E \u043E\u0442 \u0441\u0442\u043E\u043B\u0431\u0446\u043E\u0432 \u043E\u0448\u0438\u0431\u043E\u043A.\n\n"
                + "\u0413\u0440\u0430\u043D\u0438\u0446\u0430 \u2014 \u044D\u0442\u043E \u0430\u0434\u0430\u043F\u0442\u0430\u0446\u0438\u044F:\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044D \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 \u2014 \u0430\u0434\u0430\u043F\u0442\u0438\u0440\u043E\u0432\u0430\u043D\u043E \u043A \u0430\u0434\u044B\u0433\u0435\u0439\u0441\u043A\u043E\u0439 \u043C\u043E\u0440\u0444\u043E\u043B\u043E\u0433\u0438\u0438, \u0447\u0430\u0441\u0442\u044C \u0441\u043B\u043E\u0432\u0430\u0440\u043D\u043E\u0433\u043E "
                + "\u0441\u043E\u0441\u0442\u0430\u0432\u0430 \u2192 TRM, \u0435\u0441\u043B\u0438 \u0438\u0441\u043F\u043E\u043B\u044C\u0437\u043E\u0432\u0430\u043D\u043E \u043F\u0440\u0438 \u043D\u0430\u043B\u0438\u0447\u0438\u0438 \u043C\u0435\u0441\u0442\u043D\u043E\u0433\u043E \u044D\u043A\u0432\u0438\u0432\u0430\u043B\u0435\u043D\u0442\u0430 (\u04C0\u044D\u0440\u044B\u0448\u04CF \u0430\u043A\u044A\u044B\u043B)\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u044B\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 \u2014 \u0432 \u0441\u043E\u0431\u0441\u0442\u0432\u0435\u043D\u043D\u043E\u0439 \u0444\u043E\u0440\u043C\u0435 \u044F\u0437\u044B\u043A\u0430-\u0438\u0441\u0442\u043E\u0447\u043D\u0438\u043A\u0430 \u2192 MIX (\u044F\u0437\u044B\u043A \u043A\u043E\u043D\u0442\u0430\u043A\u0442\u0430: RUS)\n"
                + "\u00B7 \u0438\u0441\u043A\u0443\u0441\u0441\u0442\u0432\u0435\u043D\u043D\u0430\u0439 \u0438\u043D\u0442\u0435\u043B\u043B\u0435\u043A\u0442 \u2014 \u043D\u0435 \u044F\u0432\u043B\u044F\u0435\u0442\u0441\u044F \u0441\u0442\u0430\u043D\u0434\u0430\u0440\u0442\u043E\u043C \u043D\u0438 \u0432 \u0440\u0443\u0441\u0441\u043A\u043E\u043C, \u043D\u0438 \u0432 \u0430\u0434\u044B\u0433\u0435\u0439\u0441\u043A\u043E\u043C \u2192 INV\n\n"
                + "\u0412\u044B\u0431\u043E\u0440 \u0442\u0435\u0440\u043C\u0438\u043D\u0430 \u043D\u0435 \u0443\u043C\u0435\u043D\u044C\u0448\u0430\u0435\u0442 \u0447\u0438\u0441\u043B\u043E \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u044B\u0445 \u0441\u043B\u043E\u0432 \u2014 \u043F\u043E\u044D\u0442\u043E\u043C\u0443 \u043E\u043D \u0441\u0442\u043E\u0438\u0442 "
                + "\u043E\u0442\u0434\u0435\u043B\u044C\u043D\u043E \u043E\u0442 \u0441\u0442\u043E\u043B\u0431\u0446\u043E\u0432 \u043E\u0448\u0438\u0431\u043E\u043A, \u043F\u043E\u0441\u043B\u0435 \u0441\u0442\u043E\u043B\u0431\u0446\u0430 \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u044B\u0445 \u0441\u043B\u043E\u0432, \u0430 \u043D\u0435 \u0441\u0440\u0435\u0434\u0438 \u043D\u0438\u0445.\n\n"
                + "\u0421\u043B\u043E\u0432\u0430, \u0437\u0430\u0441\u0447\u0438\u0442\u0430\u043D\u043D\u044B\u0435 \u043A\u0430\u043A \u0432\u044B\u0431\u043E\u0440 \u0442\u0435\u0440\u043C\u0438\u043D\u0430, \u043D\u0438\u043A\u043E\u0433\u0434\u0430 \u043D\u0435 \u0432\u043F\u0438\u0441\u044B\u0432\u0430\u044E\u0442\u0441\u044F \u0432 \u0441\u0442\u043E\u043B\u0431\u0446\u044B \u044F\u0437\u044B\u043A\u043E\u0432 \u043A\u043E\u043D\u0442\u0430\u043A\u0442\u0430. "
                + "\u042D\u0442\u0438 \u0441\u0442\u043E\u043B\u0431\u0446\u044B \u2014 \u043B\u0438\u0448\u044C \u0440\u0430\u0437\u0431\u0438\u0432\u043A\u0430 \u043F\u043E \u044F\u0437\u044B\u043A\u0443-\u0438\u0441\u0442\u043E\u0447\u043D\u0438\u043A\u0443 \u0441\u043B\u043E\u0432, \u0437\u0430\u0441\u0447\u0438\u0442\u0430\u043D\u043D\u044B\u0445 \u043A\u0430\u043A \u0441\u043C\u0435\u0448\u0435\u043D\u0438\u0435 "
                + "\u043A\u043E\u0434\u043E\u0432 (MIX); \u043F\u043E\u044D\u0442\u043E\u043C\u0443 \u0438\u0445 \u0441\u0443\u043C\u043C\u0430 \u0434\u043E\u043B\u0436\u043D\u0430 \u0441\u043E\u0432\u043F\u0430\u0434\u0430\u0442\u044C \u0441\u043E \u0441\u0442\u043E\u043B\u0431\u0446\u043E\u043C \u0441\u043C\u0435\u0448\u0435\u043D\u0438\u044F \u043A\u043E\u0434\u043E\u0432.";

            Prose(en, tr, ru);
        }
        r++;

        // Instructions
        Header("Instructions", "Yönerge", "Инструкция");
        Prose(
            "On the Responses sheet, mark language compliance, semantic adequacy and Marked (I-K) "
                + "for every response, and Question 7 verdict (L) for question 7's rows only. On the "
                + "Sentences sheet, mark every sentence's verdict (G) and, for faulty sentences, count how "
                + "many words fall into each error category (H-K) and, separately, how many are term "
                + "choice (M) -- term choice is not an error. Count words from each of the four listed "
                + "contact languages (N-Q) and, for every other language combined, the total word count "
                + "(R). Columns L and S compute themselves; do not edit them. If a response's language "
                + "compliance is UYUM_YOK (no compliance), leave its sentences unlabelled -- judging a "
                + "text that is not in the target language on this scale is meaningless.\n\n"
                + "Contact-language columns take a word count, not a mark. A cell left blank or set to "
                + "zero means that language is not present in that sentence.\n\n"
                + "The Other column is the total word count for every language not on the list; no "
                + "language name is written there -- if needed, note it in the Note column.\n\n"
                + "Correct words and the total-words-in-other-languages columns compute themselves. The "
                + "total must equal the code-mixing column's own count; if it does not, a word was either "
                + "missed or counted twice.\n\n"
                // Revision 6 §10.1: the one-way link between a sentence's verdict and its word-level
                // error counts was never written down, even though it was bound to come up as soon
                // as marking actually began -- and, in miniature, already had during the trial.
                + "If you mark a sentence C1 (correct), that sentence cannot contain a word counted "
                + "under an error code (FRM, CHO, INV or MIX). The reverse is not true: a sentence "
                + "whose every word is correct can still be C2 or C3 because of word order -- word "
                + "order is a property of the sentence, not of its words. Term choice is outside this "
                + "rule; a C1 sentence can contain a term choice.\n\n"
                // Revision 6 §10.2: KISMEN was previously the one language-compliance value the
                // instructions never explained how to handle at sentence level.
                + "In a response whose language compliance is KISMEN (partial), sentences in the "
                + "target language are labelled normally; sentences in another language are left with "
                + "their verdict and word counts blank, and the situation is noted in the Note column. "
                + "In a response whose language compliance is UYUM_YOK (none), no sentence is labelled.",
            "Yanıtlar sayfasında her yanıt için dil uyumu, semantik yeterlik ve Değerlendirildi "
                + "alanlarını (I-K), yalnız 7. sorunun satırlarında 7. soru yargısını (L) doldurun. "
                + "Cümleler sayfasında her cümlenin yargısını (G) işaretleyin; hatalı cümlelerde her hata "
                + "kategorisinden kaç sözcük olduğunu sayın (H-K) ve ayrıca kaç sözcüğün terim tercihi "
                + "(M) olduğunu sayın -- terim tercihi hata değildir. Listedeki dört temas dilinden "
                + "hangisinden kaç sözcük karıştıysa sayın (N-Q), listede olmayan bütün diller için "
                + "toplam sözcük sayısını yazın (R). L ve S sütunları kendilerini hesaplar, elle "
                + "değiştirmeyin. Bir yanıtın dil uyumu UYUM_YOK ise o yanıtın cümlelerini etiketlemeyin "
                + "-- hedef dilde olmayan bir metni bu ölçekle yargılamak anlamsızdır.\n\n"
                + "Temas dili sütunlarına işaret değil sözcük sayısı yazılır. Boş bırakılan ya da sıfır "
                + "yazılan hücre, o dilin o cümlede bulunmadığı anlamına gelir.\n\n"
                + "Diğer sütunu, listede bulunmayan bütün dillerdeki başka dilde sözcük sayısının "
                + "toplamıdır; dil adı yazılmaz, gerekiyorsa Not sütununa yazılır.\n\n"
                + "Doğru sözcük ve farklı dillerdeki sözcük toplamı sütunları kendilerini hesaplar. "
                + "Toplam, kod karışımı sütunuyla aynı sayıyı vermelidir; vermiyorsa bir sözcük ya "
                + "sayılmamış ya iki kez sayılmıştır.\n\n"
                + "Bir cümleyi C1 (doğru) işaretlediysen o cümlede hata kodu (FRM, CHO, INV ya da MIX) "
                + "sayılmış sözcük bulunamaz. Bunun tersi geçerli değildir: bütün sözcükleri doğru olan "
                + "bir cümle, sözdizimi nedeniyle C2 ya da C3 olabilir -- sözdizimi cümlenin özelliğidir. "
                + "Terim tercihi bu kuralın dışındadır; C1 bir cümlede terim tercihi bulunabilir.\n\n"
                + "Dil uyumu KISMEN olan bir yanıtta, hedef dildeki cümleler normal biçimde etiketlenir; "
                + "başka dilde olan cümlelerin yargısı ve sözcük sayıları boş bırakılır ve durum Not "
                + "sütununa yazılır. Dil uyumu UYUM_YOK olan yanıtta hiçbir cümle etiketlenmez.",
            "\u041D\u0430 \u043B\u0438\u0441\u0442\u0435 \u041E\u0442\u0432\u0435\u0442\u044B \u0434\u043B\u044F \u043A\u0430\u0436\u0434\u043E\u0433\u043E \u043E\u0442\u0432\u0435\u0442\u0430 \u0437\u0430\u043F\u043E\u043B\u043D\u0438\u0442\u0435 \u0441\u043E\u043E\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0438\u0435 \u044F\u0437\u044B\u043A\u0443, \u0441\u0435\u043C\u0430\u043D\u0442\u0438\u0447\u0435\u0441\u043A\u0443\u044E "
                + "\u0430\u0434\u0435\u043A\u0432\u0430\u0442\u043D\u043E\u0441\u0442\u044C \u0438 \u0420\u0430\u0437\u043C\u0435\u0447\u0435\u043D\u043E (I-K), \u0430 \u043E\u0446\u0435\u043D\u043A\u0443 \u0432\u043E\u043F\u0440\u043E\u0441\u0430 7 (L) -- \u0442\u043E\u043B\u044C\u043A\u043E \u0434\u043B\u044F \u0441\u0442\u0440\u043E\u043A "
                + "\u0441\u0435\u0434\u044C\u043C\u043E\u0433\u043E \u0432\u043E\u043F\u0440\u043E\u0441\u0430. \u041D\u0430 \u043B\u0438\u0441\u0442\u0435 \u041F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u044F \u043E\u0442\u043C\u0435\u0447\u0430\u0439\u0442\u0435 \u043E\u0446\u0435\u043D\u043A\u0443 \u043A\u0430\u0436\u0434\u043E\u0433\u043E \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u044F (G) \u0438, \u0434\u043B\u044F "
                + "\u043E\u0448\u0438\u0431\u043E\u0447\u043D\u044B\u0445 \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0439, \u0441\u043E\u0441\u0447\u0438\u0442\u0430\u0439\u0442\u0435 \u0441\u043B\u043E\u0432\u0430 \u043F\u043E \u043A\u0430\u0436\u0434\u043E\u0439 \u043A\u0430\u0442\u0435\u0433\u043E\u0440\u0438\u0438 \u043E\u0448\u0438\u0431\u043E\u043A (H-K) \u0438 \u043E\u0442\u0434\u0435\u043B\u044C\u043D\u043E -- "
                + "\u0441\u043A\u043E\u043B\u044C\u043A\u043E \u0438\u0437 \u043D\u0438\u0445 \u044F\u0432\u043B\u044F\u044E\u0442\u0441\u044F \u0432\u044B\u0431\u043E\u0440\u043E\u043C \u0442\u0435\u0440\u043C\u0438\u043D\u0430 (M) -- \u0432\u044B\u0431\u043E\u0440 \u0442\u0435\u0440\u043C\u0438\u043D\u0430 \u043D\u0435 \u044F\u0432\u043B\u044F\u0435\u0442\u0441\u044F \u043E\u0448\u0438\u0431\u043A\u043E\u0439. "
                + "\u0421\u043E\u0441\u0447\u0438\u0442\u0430\u0439\u0442\u0435 \u0441\u043B\u043E\u0432\u0430 \u0438\u0437 \u043A\u0430\u0436\u0434\u043E\u0433\u043E \u0438\u0437 \u0447\u0435\u0442\u044B\u0440\u0451\u0445 \u044F\u0437\u044B\u043A\u043E\u0432 \u043A\u043E\u043D\u0442\u0430\u043A\u0442\u0430 \u0438\u0437 \u0441\u043F\u0438\u0441\u043A\u0430 (N-Q) \u0438, \u0434\u043B\u044F \u0432\u0441\u0435\u0445 \u043E\u0441\u0442\u0430\u043B\u044C\u043D\u044B\u0445 \u044F\u0437\u044B\u043A\u043E\u0432 \u0432\u043C\u0435\u0441\u0442\u0435, "
                + "\u043E\u0431\u0449\u0435\u0435 \u0447\u0438\u0441\u043B\u043E \u0441\u043B\u043E\u0432 (R). \u0421\u0442\u043E\u043B\u0431\u0446\u044B L \u0438 S \u0432\u044B\u0447\u0438\u0441\u043B\u044F\u044E\u0442\u0441\u044F \u0441\u0430\u043C\u0438, \u043D\u0435 \u0440\u0435\u0434\u0430\u043A\u0442\u0438\u0440\u0443\u0439\u0442\u0435 \u0438\u0445. \u0415\u0441\u043B\u0438 \u0441\u043E\u043E\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0438\u0435 "
                + "\u044F\u0437\u044B\u043A\u0443 \u043E\u0442\u0432\u0435\u0442\u0430 \u2014 UYUM_YOK, \u043D\u0435 \u0440\u0430\u0437\u043C\u0435\u0447\u0430\u0439\u0442\u0435 \u0435\u0433\u043E \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u044F.\n\n"
                + "\u0412 \u0441\u0442\u043E\u043B\u0431\u0446\u044B \u044F\u0437\u044B\u043A\u043E\u0432 \u043A\u043E\u043D\u0442\u0430\u043A\u0442\u0430 \u0432\u043F\u0438\u0441\u044B\u0432\u0430\u0435\u0442\u0441\u044F \u043A\u043E\u043B\u0438\u0447\u0435\u0441\u0442\u0432\u043E \u0441\u043B\u043E\u0432, \u0430 \u043D\u0435 \u043E\u0442\u043C\u0435\u0442\u043A\u0430. \u041F\u0443\u0441\u0442\u0430\u044F \u044F\u0447\u0435\u0439\u043A\u0430 \u0438\u043B\u0438 \u043D\u043E\u043B\u044C "
                + "\u043E\u0437\u043D\u0430\u0447\u0430\u044E\u0442, \u0447\u0442\u043E \u044D\u0442\u043E\u0433\u043E \u044F\u0437\u044B\u043A\u0430 \u0432 \u044D\u0442\u043E\u043C \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0438 \u043D\u0435\u0442.\n\n"
                + "\u0421\u0442\u043E\u043B\u0431\u0435\u0446 \u00AB\u041F\u0440\u043E\u0447\u0438\u0435\u00BB \u2014 \u044D\u0442\u043E \u0441\u0443\u043C\u043C\u0430 \u0441\u043B\u043E\u0432 \u043D\u0430 \u0432\u0441\u0435\u0445 \u044F\u0437\u044B\u043A\u0430\u0445, \u043D\u0435 \u0432\u0445\u043E\u0434\u044F\u0449\u0438\u0445 \u0432 \u0441\u043F\u0438\u0441\u043E\u043A; \u043D\u0430\u0437\u0432\u0430\u043D\u0438\u0435 \u044F\u0437\u044B\u043A\u0430 \u043D\u0435 "
                + "\u043F\u0438\u0448\u0435\u0442\u0441\u044F, \u043F\u0440\u0438 \u043D\u0435\u043E\u0431\u0445\u043E\u0434\u0438\u043C\u043E\u0441\u0442\u0438 \u2014 \u0432 \u0441\u0442\u043E\u043B\u0431\u0446\u0435 \u041F\u0440\u0438\u043C\u0435\u0447\u0430\u043D\u0438\u0435.\n\n"
                + "\u0421\u0442\u043E\u043B\u0431\u0446\u044B \u00AB\u041F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u044B\u0435 \u0441\u043B\u043E\u0432\u0430\u00BB \u0438 \u00AB\u0412\u0441\u0435\u0433\u043E \u0441\u043B\u043E\u0432 \u043D\u0430 \u0434\u0440\u0443\u0433\u0438\u0445 \u044F\u0437\u044B\u043A\u0430\u0445\u00BB \u0432\u044B\u0447\u0438\u0441\u043B\u044F\u044E\u0442\u0441\u044F \u0441\u0430\u043C\u0438. \u0418\u0442\u043E\u0433 \u0434\u043E\u043B\u0436\u0435\u043D "
                + "\u0441\u043E\u0432\u043F\u0430\u0434\u0430\u0442\u044C \u0441\u043E \u0441\u0442\u043E\u043B\u0431\u0446\u043E\u043C \u0441\u043C\u0435\u0448\u0435\u043D\u0438\u044F \u043A\u043E\u0434\u043E\u0432; \u0435\u0441\u043B\u0438 \u043D\u0435 \u0441\u043E\u0432\u043F\u0430\u0434\u0430\u0435\u0442, \u0441\u043B\u043E\u0432\u043E \u043B\u0438\u0431\u043E \u043D\u0435 \u043F\u043E\u0441\u0447\u0438\u0442\u0430\u043D\u043E, "
                + "\u043B\u0438\u0431\u043E \u043F\u043E\u0441\u0447\u0438\u0442\u0430\u043D\u043E \u0434\u0432\u0430\u0436\u0434\u044B.\n\n"
                + "\u0415\u0441\u043B\u0438 \u0432\u044B \u043E\u0442\u043C\u0435\u0442\u0438\u043B\u0438 \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0435 \u043A\u0430\u043A C1 (\u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u043E\u0435), \u0432 \u044D\u0442\u043E\u043C \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0438 \u043D\u0435 \u043C\u043E\u0436\u0435\u0442 \u0431\u044B\u0442\u044C "
                + "\u0441\u043B\u043E\u0432\u0430, \u0437\u0430\u0441\u0447\u0438\u0442\u0430\u043D\u043D\u043E\u0433\u043E \u043F\u043E\u0434 \u043A\u043E\u0434\u043E\u043C \u043E\u0448\u0438\u0431\u043A\u0438 (FRM, CHO, INV \u0438\u043B\u0438 MIX). \u041E\u0431\u0440\u0430\u0442\u043D\u043E\u0435 \u043D\u0435 \u0432\u0435\u0440\u043D\u043E: "
                + "\u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0435, \u0432 \u043A\u043E\u0442\u043E\u0440\u043E\u043C \u0432\u0441\u0435 \u0441\u043B\u043E\u0432\u0430 \u043F\u0440\u0430\u0432\u0438\u043B\u044C\u043D\u044B, \u0432\u0441\u0451 \u0436\u0435 \u043C\u043E\u0436\u0435\u0442 \u0431\u044B\u0442\u044C C2 \u0438\u043B\u0438 C3 \u0438\u0437-\u0437\u0430 "
                + "\u043F\u043E\u0440\u044F\u0434\u043A\u0430 \u0441\u043B\u043E\u0432 -- \u043F\u043E\u0440\u044F\u0434\u043E\u043A \u0441\u043B\u043E\u0432 \u044F\u0432\u043B\u044F\u0435\u0442\u0441\u044F \u0441\u0432\u043E\u0439\u0441\u0442\u0432\u043E\u043C \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u044F, \u0430 \u043D\u0435 \u0441\u043B\u043E\u0432. \u0412\u044B\u0431\u043E\u0440 "
                + "\u0442\u0435\u0440\u043C\u0438\u043D\u0430 \u043D\u0435 \u043F\u043E\u0434\u043F\u0430\u0434\u0430\u0435\u0442 \u043F\u043E\u0434 \u044D\u0442\u043E \u043F\u0440\u0430\u0432\u0438\u043B\u043E; \u0432 \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0438 C1 \u043C\u043E\u0436\u0435\u0442 \u0431\u044B\u0442\u044C \u0432\u044B\u0431\u043E\u0440 \u0442\u0435\u0440\u043C\u0438\u043D\u0430.\n\n"
                + "\u0412 \u043E\u0442\u0432\u0435\u0442\u0435, \u0433\u0434\u0435 \u0441\u043E\u043E\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0438\u0435 \u044F\u0437\u044B\u043A\u0443 -- KISMEN (\u0447\u0430\u0441\u0442\u0438\u0447\u043D\u043E\u0435), \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u044F \u043D\u0430 \u0446\u0435\u043B\u0435\u0432\u043E\u043C "
                + "\u044F\u0437\u044B\u043A\u0435 \u0440\u0430\u0437\u043C\u0435\u0447\u0430\u044E\u0442\u0441\u044F \u043E\u0431\u044B\u0447\u043D\u044B\u043C \u043E\u0431\u0440\u0430\u0437\u043E\u043C; \u0443 \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0439 \u043D\u0430 \u0434\u0440\u0443\u0433\u043E\u043C \u044F\u0437\u044B\u043A\u0435 \u043E\u0446\u0435\u043D\u043A\u0430 \u0438 "
                + "\u043A\u043E\u043B\u0438\u0447\u0435\u0441\u0442\u0432\u043E \u0441\u043B\u043E\u0432 \u043E\u0441\u0442\u0430\u0432\u043B\u044F\u044E\u0442\u0441\u044F \u043F\u0443\u0441\u0442\u044B\u043C\u0438, \u0430 \u0441\u0438\u0442\u0443\u0430\u0446\u0438\u044F \u043E\u0442\u043C\u0435\u0447\u0430\u0435\u0442\u0441\u044F \u0432 \u0441\u0442\u043E\u043B\u0431\u0446\u0435 \u041F\u0440\u0438\u043C\u0435\u0447\u0430\u043D\u0438\u0435. "
                + "\u0412 \u043E\u0442\u0432\u0435\u0442\u0435, \u0433\u0434\u0435 \u0441\u043E\u043E\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0438\u0435 \u044F\u0437\u044B\u043A\u0443 -- UYUM_YOK (\u043D\u0435\u0442), \u043D\u0438 \u043E\u0434\u043D\u043E \u043F\u0440\u0435\u0434\u043B\u043E\u0436\u0435\u043D\u0438\u0435 \u043D\u0435 "
                + "\u0440\u0430\u0437\u043C\u0435\u0447\u0430\u0435\u0442\u0441\u044F.");

        var worksheet = new Worksheet(
            new SheetFormatProperties { DefaultRowHeight = 15, DefaultColumnWidth = 30 },
            FixedColumns((1, 1, 40), (2, 2, 45), (3, 3, 45), (4, 4, 45)),
            sheetData,
            DefaultProtection(),
            DefaultMargins());

        part.Worksheet = worksheet;
    }
}
