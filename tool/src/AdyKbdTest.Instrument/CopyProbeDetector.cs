namespace AdyKbdTest.Instrument;

/// <summary>
/// The copying probe (revision 1, §6), moved from a human-marked workbook column to a mechanical
/// check the tool runs itself. Question 5 hands back the Turkish translation of question 4's
/// Adyghe source paragraph with one deliberate change at a single clause: "düğün" (wedding)
/// becomes "doğum günü" (birthday). Whether a tool's Adyghe answer to question 5 contains the
/// *original* wedding wording or the *changed* birthday wording at that spot indicates whether it
/// copied question 4's paragraph back rather than translating the new (question 5) input.
///
/// This is inherently a best-effort textual heuristic, not a guaranteed-correct parse: different
/// tools phrase both the wedding and the birthday clause differently (observed in
/// data/raw/round1: "инысащэ", "тинысащ", "икъызыхъугъэ мафэм", "икъыщыхъугъэ мафэм",
/// "дунаим къызытехъугъэ мафэм"...), which is exactly why the result is an observation ("bu bir
/// gözlemdir, hüküm değil") with an explicit "unclear" outcome, not a forced binary judgement.
/// </summary>
public static class CopyProbeDetector
{
    /// <summary>The wedding root as it appears in every observed rendering of the unchanged clause (нысащэ, инысащэ, тинысащ...).</summary>
    private const string OriginalMarker = "нысащ"; // "нысащ"

    /// <summary>The "day [something] happened/was born" root shared by every observed birthday rendering (къызыхъугъэ мафэ, къыщыхъугъэ мафэ, къызытехъугъэ мафэ...).</summary>
    private const string ChangedMarker = "хъугъэ мафэ"; // "хъугъэ мафэ" -- every character spelled by code point to avoid a repeat of the schwa (U+04D8) vs. Cyrillic e (U+044D) typo this string previously had

    public static CopyProbeResult Detect(string question5RepairedAnswerText)
    {
        var hasOriginal = question5RepairedAnswerText.Contains(OriginalMarker, StringComparison.OrdinalIgnoreCase);
        var hasChanged = question5RepairedAnswerText.Contains(ChangedMarker, StringComparison.OrdinalIgnoreCase);

        if (hasOriginal && !hasChanged) return CopyProbeResult.OZGUN_GERI_VERDI;
        if (hasChanged && !hasOriginal) return CopyProbeResult.DEGISIKLIGI_YANSITTI;
        return CopyProbeResult.BELIRSIZ;
    }
}
