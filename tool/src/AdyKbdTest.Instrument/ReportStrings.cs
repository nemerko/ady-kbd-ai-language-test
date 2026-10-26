namespace AdyKbdTest.Instrument;

/// <summary>One language of the report page (revision 18 §2).</summary>
public sealed record ReportLanguage(string Code, string FileName, string HtmlLang, char DecimalSeparator);

/// <summary>
/// Every piece of interface text on the report page, in one table: key -> text per language
/// (revision 18 §2). Adding a language is adding an entry to <see cref="Languages"/> and one more
/// argument to each <c>L(...)</c> row; nothing else knows how many languages there are.
///
/// Category words (language compliance, semantic adequacy, sentence verdicts, error codes) are NOT
/// in this table: they come from <see cref="CodingSchemeText"/>, so the Russian page uses exactly
/// the Russian the workbooks use. Only the page's own interface text is here.
/// </summary>
public static class ReportStrings
{
    /// <summary>The page languages, in the order of the arguments of <c>L(...)</c> below.</summary>
    public static readonly IReadOnlyList<ReportLanguage> Languages =
    [
        new("en", "report.html", "en", '.'),
        new("tr", "report.tr.html", "tr", ','),
        new("ru", "report.ru.html", "ru", ','),
    ];

    private static IReadOnlyDictionary<string, string> L(params string[] texts)
    {
        if (texts.Length != Languages.Count)
        {
            throw new InvalidOperationException($"A report string needs {Languages.Count} texts, got {texts.Length}.");
        }
        return Languages.Select((lang, i) => (lang.Code, texts[i])).ToDictionary(x => x.Code, x => x.Item2);
    }

    /// <summary>Key -> language code -> text. Placeholders are written <c>{name}</c>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["page_title"] = L(
                "Pilot test results: the Adyghe score of five tools",
                "Pilot test sonuçları: beş aracın Adıgece puanı",
                "Результаты пилотных тестов: оценка пяти инструментов по адыгейскому языку"),
            ["intro"] = L(
                "Score 0–100: the equally weighted mean, over question groups and over tests, of the percentage of error-free words. A response outside the measurement (not in the target language and not addressing the question) scores 0 for that question.",
                "Puan 0–100: hatasız sözcük yüzdesinin soru gruplarına ve testlere göre eşit ağırlıklı ortalaması. Ölçüm dışı yanıt (hedef dilde olmayan ve soruyu karşılamayan) o soru için 0 alır.",
                "Оценка от 0 до 100: среднее с равными весами по группам вопросов и по тестам доли слов без ошибок. Ответ вне измерения (не на целевом языке и не отвечающий на вопрос) получает за этот вопрос 0."),
            ["h_ranking"] = L(
                "1. Overall ranking (mean of the {n} tests)",
                "1. Genel sıralama ({n} testin ortalaması)",
                "1. Общий рейтинг (среднее по {n} тестам)"),
            ["tests_label"] = L("tests", "testler", "тесты"),
            ["mean_tip"] = L(
                "mean of the five tools {x}",
                "beş araç ortalaması {x}",
                "среднее по пяти инструментам {x}"),
            ["h_card"] = L(
                "2. Score card: test by test",
                "2. Karne: test test puanlar",
                "2. Табель: оценки по тестам"),
            ["card_note"] = L(
                "Darker means a higher score. Dashed outline: in that test a question group scored 0 (the task was not done).",
                "Koyu renk daha yüksek puan. Kesikli çerçeve: o testte bir soru grubu 0 puan (görev yapılmadı).",
                "Чем темнее, тем выше оценка. Пунктирная рамка: в этом тесте группа вопросов получила 0 (задание не выполнено)."),
            ["th_tool"] = L("Tool", "Araç", "Инструмент"),
            ["th_test"] = L("Test {n}", "Test {n}", "Тест {n}"),
            ["th_overall"] = L("Overall", "Genel", "Итого"),
            ["th_rank"] = L("Rank", "Sıra", "Место"),
            ["scale_low"] = L("low", "düşük", "низкая"),
            ["scale_high"] = L("high", "yüksek", "высокая"),
            ["scale_bands"] = L(
                "(bands: <70 · 70–80 · 80–85 · ≥85)",
                "(bantlar: <70 · 70–80 · 80–85 · ≥85)",
                "(диапазоны: <70 · 70–80 · 80–85 · ≥85)"),
            ["h_dev"] = L(
                "3. Over and under: deviation from the five-tool mean in each question group",
                "3. Fazla ve eksik: her soru grubunda beş araç ortalamasından sapma",
                "3. Выше и ниже: отклонение от среднего по пяти инструментам в каждой группе вопросов"),
            ["dev_note"] = L(
                "A bar to the right is above the mean, to the left below it (difference in score points). Mean of the {n} tests.",
                "Sağa uzanan çubuk ortalamanın üstünde, sola uzanan altında (puan farkı). {n} testin ortalaması.",
                "Столбец вправо — выше среднего, влево — ниже (разница в баллах). Среднее по {n} тестам."),
            ["group_q1_3"] = L("free response", "serbest yanıt", "свободный ответ"),
            ["group_q4"] = L("Adyghe→Turkish", "Adıgece→Türkçe", "адыгейский→турецкий"),
            ["group_q5"] = L("Turkish→Adyghe", "Türkçe→Adıgece", "турецкий→адыгейский"),
            ["group_q6_7"] = L("summary, comparison", "özet, karşılaştırma", "резюме, сравнение"),
            ["dev_mean"] = L("mean", "ortalama", "среднее"),
            ["dev_tip_score"] = L(
                "group score {s} (mean {m})",
                "grup puanı {s} (ortalama {m})",
                "оценка группы {s} (среднее {m})"),
            ["noscript"] = L(
                "The charts in section 3 need JavaScript; sections 1 and 2 are plain text.",
                "3. bölümdeki grafikler JavaScript ister; 1. ve 2. bölüm düz metindir.",
                "Графики в разделе 3 требуют JavaScript; разделы 1 и 2 — обычный текст."),
            ["h_footnotes"] = L(
                "Notes on the numbers",
                "Sayılara dair dipnotlar",
                "Примечания к числам"),
            ["fn_excluded"] = L(
                "{tool}, Test {t}, S{q}: outside the measurement — language compliance {lc}, semantic adequacy {sem} → score 0",
                "{tool}, Test {t}, S{q}: ölçüm dışı — dil uyumu {lc}, anlam {sem} → 0 puan",
                "{tool}, тест {t}, S{q}: вне измерения — соответствие языку {lc}, адекватность {sem} → 0 баллов"),
            ["fn_zero"] = L(
                "{tool}, Test {t}, group {g}: {qs} score 0",
                "{tool}, Test {t}, {g} grubu: {qs} puanı 0",
                "{tool}, тест {t}, группа {g}: оценка {qs} равна 0"),
            ["fn_tie"] = L(
                "{scope}: {tools} have the same score ({s}); they are ordered by tool number.",
                "{scope}: {tools} aynı puanı aldı ({s}); araç numarasına göre sıralandı.",
                "{scope}: {tools} имеют одинаковую оценку ({s}); упорядочены по номеру инструмента."),
            ["scope_overall"] = L("Overall ranking", "Genel sıralama", "Общий рейтинг"),
            ["scope_test"] = L("Test {t}", "Test {t}", "Тест {t}"),
            ["fn_none"] = L(
                "No response was excluded, no question scored 0 and no ranks tied.",
                "Hiçbir yanıt ölçüm dışı kalmadı, hiçbir soru 0 puan almadı ve eşit sıra yok.",
                "Ни один ответ не исключён, ни один вопрос не получил 0 и равных мест нет."),
            ["h_notes"] = L("Evaluator notes", "Değerlendirici notları", "Заметки оценщика"),
            ["notes_intro"] = L(
                "The notes are the text the evaluator wrote in the workbook; they are shown as written and are not translated.",
                "Notlar, değerlendiricinin kitaba yazdığı metindir; yazıldığı gibi gösterilir, çevrilmez.",
                "Заметки — это текст, который оценщик написал в рабочей книге; они показаны как есть и не переводятся."),
            ["h_notes_responses"] = L("Responses sheet", "Yanıtlar sayfası", "лист Ответы"),
            ["h_notes_standard"] = L(
                "Sentences sheet: standard notes (counts by tool and test)",
                "Cümleler sayfası: standart notlar (araç ve teste göre sayım)",
                "лист Предложения: стандартные заметки (количество по инструментам и тестам)"),
            ["h_notes_oneoff"] = L(
                "Sentences sheet: one-off notes",
                "Cümleler sayfası: tek seferlik notlar",
                "лист Предложения: единичные заметки"),
            ["th_total"] = L("Total", "Toplam", "Всего"),
            ["sentence_no"] = L("sentence {n}", "cümle {n}", "предложение {n}"),
            ["none"] = L("(none)", "(yok)", "(нет)"),
            ["src_label"] = L("Source", "Kaynak", "Источник"),
            ["src_generated"] = L("generated", "üretim", "создано"),
        };

    public static string Get(string key, string languageCode) => All[key][languageCode];

    /// <summary>Replaces <c>{name}</c> placeholders.</summary>
    public static string Fill(string template, params (string Name, string Value)[] values)
    {
        foreach (var (name, value) in values) template = template.Replace("{" + name + "}", value, StringComparison.Ordinal);
        return template;
    }
}
