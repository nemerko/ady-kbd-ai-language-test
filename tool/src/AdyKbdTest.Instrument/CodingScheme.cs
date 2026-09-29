namespace AdyKbdTest.Instrument;

/// <summary>
/// Sentence-level verdict (Cümleler sheet, column G). Every sentence the analyser splits out is
/// labelled, not just faulty ones.
/// </summary>
public enum SentenceVerdict
{
    Dogru,
    YanlisAmaAnlasilir,
    YanlisVeAnlamsiz,
}

/// <summary>
/// Word-error category (Cümleler sheet: error codes in columns H-K, term choice in column M --
/// revision 4 §1 moved term choice out of the contiguous error block). Revision 1: no longer a
/// per-word label -- each sentence carries a *count* per category. TRM (term choice) is
/// deliberately not an error and is excluded from error totals.
/// </summary>
public enum WordCode
{
    FRM,
    CHO,
    INV,
    MIX,
    TRM,
}

/// <summary>A contact language that can appear mixed into a sentence (Cümleler sheet, columns N-Q flags). Revision 3: a free-text "other language" column exists alongside these four for anything not on this list (§4) -- this enum stays closed deliberately, it is not meant to be exhaustive.</summary>
public enum ContactLanguage
{
    KBD,
    RUS,
    TUR,
    ENG,

    /// <summary>Every other language combined, as a single count (Sentences sheet, column R). No language name is ever recorded for it -- see the tool profile's own note in the Note column instead (revision 4 §1, revision 5 §6).</summary>
    OTHER,
}

/// <summary>Response-level: does the answer stay in the target language throughout? (Yanıtlar sheet, column I.)</summary>
public enum LanguageComplianceWord
{
    Tam,
    Kismen,
    UyumYok,
}

/// <summary>Response-level: how well the answer addresses what was asked (Yanıtlar sheet, column J).</summary>
public enum SemanticAdequacyWord
{
    Karsiladi,
    KismenKarsiladi,
    Alakasiz,
}

/// <summary>Response-level: has an evaluator marked this response at all? (Yanıtlar sheet, column K.)</summary>
public enum MarkedWord
{
    Evet,
    Hayir,
}

/// <summary>
/// Question 5 (Turkish -> Adyghe; the copying probe) result. Revision 1: computed mechanically by
/// the tool (see <see cref="CopyProbeDetector"/>), not marked by an evaluator -- there is no
/// workbook column for it any more.
/// </summary>
public enum CopyProbeResult
{
    OZGUN_GERI_VERDI,
    DEGISIKLIGI_YANSITTI,
    BELIRSIZ,
}

/// <summary>
/// Question 7 (difference between the two paragraphs) verdict -- the copying probe's second leg
/// (revision 3, §5). Unlike question 5, this needs a human judgement call (did the tool find a
/// real difference, invent one, or fail to compare at all?), so it lives as a Yanıtlar-sheet
/// dropdown, filled in only on that question's rows.
/// </summary>
public enum Question7Verdict
{
    FARKI_DOGRU_BULDU,
    FARK_YOK_DEDI,
    FARKI_YANLIS_BULDU,
}

/// <summary>
/// The three-language (EN/TR/RU) display text and, where applicable, the exact stored dropdown
/// value for one coding-scheme option. Workbook dropdowns store the TR/RU compound string itself,
/// not a bare code, per revision 1 §4 ("açılır listelerde kod değil kelime görünür"). "Code" is
/// the underlying identifier (C1, FRM, TAM, ...) shown alongside it in the Info sheet's dictionary
/// only, per revision 3 §1, so an evaluator can connect a dropdown's TR/RU wording to the codes
/// that appear in reports and summaries -- it is never itself a stored cell value.
/// </summary>
public sealed record CodeEntry(string Code, string En, string Tr, string Ru)
{
    /// <summary>The exact string stored in the cell / offered in the dropdown: "tr / ru".</summary>
    public string DropdownValue => $"{Tr} / {Ru}";
}

/// <summary>Every coding-scheme option's three-language text, in one place so the Info sheet's dictionary, the workbook's dropdowns and the report all read from the same source.</summary>
public static class CodingSchemeText
{
    public static readonly IReadOnlyDictionary<SentenceVerdict, CodeEntry> SentenceVerdicts = new Dictionary<SentenceVerdict, CodeEntry>
    {
        [SentenceVerdict.Dogru] = new("C1", "correct", "doğru", "правильно"),
        [SentenceVerdict.YanlisAmaAnlasilir] = new("C2", "faulty but comprehensible", "yanlış ama anlaşılır", "понятно"),
        [SentenceVerdict.YanlisVeAnlamsiz] = new("C3", "faulty and incomprehensible", "yanlış ve anlamsız", "непонятно"),
    };

    public static readonly IReadOnlyDictionary<WordCode, CodeEntry> WordCodes = new Dictionary<WordCode, CodeEntry>
    {
        [WordCode.FRM] = new("FRM", "wrong form", "biçim hatası", "ошибка формы"),
        [WordCode.CHO] = new("CHO", "wrong word choice", "yanlış sözcük", "неверный выбор слова"),
        [WordCode.INV] = new("INV", "invented word", "uydurma", "выдуманное слово"),
        [WordCode.MIX] = new("MIX", "code mixing", "karışma", "смешение кодов"),
        [WordCode.TRM] = new("TRM", "term choice (not an error)", "terim tercihi (hata değil)", "выбор термина (не ошибка)"),
    };

    public static readonly IReadOnlyDictionary<ContactLanguage, CodeEntry> ContactLanguages = new Dictionary<ContactLanguage, CodeEntry>
    {
        [ContactLanguage.KBD] = new("KBD", "Kabardian", "Kabartayca", "кабардинский"),
        [ContactLanguage.RUS] = new("RUS", "Russian", "Rusça", "русский"),
        [ContactLanguage.TUR] = new("TUR", "Turkish", "Türkçe", "турецкий"),
        [ContactLanguage.ENG] = new("ENG", "English", "İngilizce", "английский"),
        // Same Russian word the Sentences sheet's own R column header uses (revision 5 §6): a
        // one-off hand-written FourCol call here previously said "Другой", which disagreed with
        // the column header an evaluator actually sees ("Прочие") -- kept in one place now.
        [ContactLanguage.OTHER] = new("OTHER", "Other", "Diğer", "Прочие"),
    };

    public static readonly IReadOnlyDictionary<LanguageComplianceWord, CodeEntry> LanguageCompliance = new Dictionary<LanguageComplianceWord, CodeEntry>
    {
        [LanguageComplianceWord.Tam] = new("TAM", "full", "tam", "полное"),
        [LanguageComplianceWord.Kismen] = new("KISMEN", "partial", "kısmen", "частичное"),
        [LanguageComplianceWord.UyumYok] = new("UYUM_YOK", "none", "uyum yok", "нет"),
    };

    public static readonly IReadOnlyDictionary<SemanticAdequacyWord, CodeEntry> SemanticAdequacy = new Dictionary<SemanticAdequacyWord, CodeEntry>
    {
        [SemanticAdequacyWord.Karsiladi] = new("KARSILADI", "met the request", "karşıladı", "отвечает"),
        [SemanticAdequacyWord.KismenKarsiladi] = new("KISMEN_KARSILADI", "partially met it", "kısmen karşıladı", "частично"),
        [SemanticAdequacyWord.Alakasiz] = new("ALAKASIZ", "unrelated", "alakasız", "не по теме"),
    };

    public static readonly IReadOnlyDictionary<MarkedWord, CodeEntry> Marked = new Dictionary<MarkedWord, CodeEntry>
    {
        [MarkedWord.Evet] = new("EVET", "yes", "evet", "да"),
        [MarkedWord.Hayir] = new("HAYIR", "no", "hayır", "нет"),
    };

    public static readonly IReadOnlyDictionary<Question7Verdict, CodeEntry> Question7Verdicts = new Dictionary<Question7Verdict, CodeEntry>
    {
        [Question7Verdict.FARKI_DOGRU_BULDU] = new("FARKI_DOGRU_BULDU", "processed both texts", "iki metni de işlemiştir", "обработал оба текста"),
        [Question7Verdict.FARK_YOK_DEDI] = new("FARK_YOK_DEDI", "did not align the texts", "metinleri hizalamamıştır", "не сопоставил тексты"),
        [Question7Verdict.FARKI_YANLIS_BULDU] = new("FARKI_YANLIS_BULDU", "invented a difference that is not there (hallucination)", "olmayan bir fark uydurmuştur (halüsinasyon)", "придумал несуществующее различие (галлюцинация)"),
    };

    public static string[] DropdownValues<TEnum>(IReadOnlyDictionary<TEnum, CodeEntry> map) where TEnum : struct, Enum =>
        [.. map.Values.Select(e => e.DropdownValue)];

    public static TEnum? ParseDropdownValue<TEnum>(IReadOnlyDictionary<TEnum, CodeEntry> map, string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var (key, entry) in map)
        {
            if (string.Equals(entry.DropdownValue, value.Trim(), StringComparison.Ordinal)) return key;
        }
        return null;
    }
}
