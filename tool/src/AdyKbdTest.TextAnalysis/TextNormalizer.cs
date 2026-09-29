using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

public sealed record NormalizeResult(string NormalizedText, bool WasNfc, IReadOnlyList<Finding> Findings);

/// <summary>
/// NFC-normalises the markdown-cleaned text and flags the two decomposed sequences that matter
/// most for Cyrillic: и+combining-breve standing in for й, and е+combining-diaeresis for ё.
/// Offsets in the returned findings are into the pre-normalisation (markdown-cleaned) text, because
/// that is the last point at which those decomposed sequences are still visible as two characters.
/// </summary>
public static partial class TextNormalizer
{
    [GeneratedRegex("[иИ]̆")]
    private static partial Regex DetachedIRegex();

    [GeneratedRegex("[еЕ]̈")]
    private static partial Regex DetachedERegex();

    public static NormalizeResult Normalize(string cleanedText)
    {
        var findings = new List<Finding>();

        foreach (Match m in DetachedIRegex().Matches(cleanedText))
        {
            var expected = m.Value[0] == 'И' ? "Й" : "й";
            findings.Add(new Finding
            {
                Code = FindingCodes.Normalization,
                Severity = FindingSeverity.Medium,
                Offset = m.Index,
                Length = m.Length,
                Found = m.Value,
                Expected = expected,
                Note = "и + combining breve (U+0306) instead of precomposed й",
            });
        }

        foreach (Match m in DetachedERegex().Matches(cleanedText))
        {
            var expected = m.Value[0] == 'Е' ? "Ё" : "ё";
            findings.Add(new Finding
            {
                Code = FindingCodes.Normalization,
                Severity = FindingSeverity.Medium,
                Offset = m.Index,
                Length = m.Length,
                Found = m.Value,
                Expected = expected,
                Note = "е + combining diaeresis (U+0308) instead of precomposed ё",
            });
        }

        var wasNfc = cleanedText.IsNormalized(NormalizationForm.FormC);
        if (!wasNfc && findings.Count == 0)
        {
            findings.Add(new Finding
            {
                Code = FindingCodes.Normalization,
                Severity = FindingSeverity.Medium,
                Offset = 0,
                Length = cleanedText.Length,
                Found = "(response text)",
                Expected = "NFC",
                Note = "response is not in NFC form, no specific detached sequence recognised",
            });
        }

        var normalized = cleanedText.Normalize(NormalizationForm.FormC);
        return new NormalizeResult(normalized, wasNfc, findings);
    }
}
