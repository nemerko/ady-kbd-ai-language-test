using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

public sealed record RepairResult(string RepairedText, IReadOnlyList<Finding> Findings);

/// <summary>
/// Produces a counting-only copy of the response: palochka look-alikes inside Cyrillic-dominant
/// words are turned into the real palochka, and Unicode Cf (format, invisible) characters are
/// dropped, everywhere. The original (normalised) text this runs on is never itself mutated in
/// place — callers keep both the input and this result. Every change is recorded as a finding;
/// nothing is repaired silently.
///
/// Repair runs before sentence/token splitting (see TextAnalyzer) so that a look-alike standing in
/// for punctuation, such as "|" for palochka, does not fragment tokenisation.
///
/// "Cyrillic-dominant" is decided per whitespace-delimited chunk (the same split tokenisation will
/// redo later): a chunk with strictly more Cyrillic than Latin letters is in scope for look-alike
/// repair, so "Google-м" (6 Latin vs 1 Cyrillic) is left alone while "Iофыгъу" (1 Latin vs
/// 6 Cyrillic) is repaired. Offsets in the returned findings are into <paramref name="normalizedText"/>.
/// </summary>
public static partial class Repairer
{
    [GeneratedRegex(@"\S+")]
    private static partial Regex ChunkRegex();

    public static RepairResult Repair(string normalizedText, LanguageProfile profile)
    {
        var findings = new List<Finding>();
        var isCyrillicDominantAt = new bool[normalizedText.Length];

        foreach (Match chunk in ChunkRegex().Matches(normalizedText))
        {
            var (cyr, lat, _) = ScriptClassifier.CountLetters(chunk.Value);
            if (cyr > lat)
            {
                for (var i = chunk.Index; i < chunk.Index + chunk.Length; i++)
                {
                    isCyrillicDominantAt[i] = true;
                }
            }
        }

        var sb = new StringBuilder(normalizedText.Length);
        var palochkaUpper = profile.Alphabet.CharactersOfNote.PalochkaUpper;
        var palochkaLower = profile.Alphabet.CharactersOfNote.PalochkaLower;

        for (var i = 0; i < normalizedText.Length; i++)
        {
            var c = normalizedText[i];

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                findings.Add(new Finding
                {
                    Code = FindingCodes.Invisible,
                    Severity = FindingSeverity.Medium,
                    Offset = i,
                    Length = 1,
                    Found = $"U+{(int)c:X4}",
                    Expected = "(removed)",
                    Note = "Unicode Cf (format) character dropped from the counting copy",
                });
                continue;
            }

            var chStr = c.ToString();
            if (isCyrillicDominantAt[i] && profile.IsPalochkaLookAlike(chStr))
            {
                var expected = char.IsUpper(c) ? palochkaUpper : palochkaLower;
                findings.Add(new Finding
                {
                    Code = FindingCodes.PalochkaLookAlike,
                    Severity = FindingSeverity.High,
                    Offset = i,
                    Length = 1,
                    Found = chStr,
                    Expected = expected,
                });
                sb.Append(expected);
                continue;
            }

            sb.Append(c);
        }

        return new RepairResult(sb.ToString(), findings);
    }
}
