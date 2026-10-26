using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// <paramref name="SourceIndex"/> has one entry per character of <paramref name="RepairedText"/>:
/// the index of the same character in the text that was repaired (the normalised text). Repair only
/// ever replaces a character 1:1 or deletes one, so this is a complete, exact position map
/// (revision 14 §3.1) -- it is what lets a sentence found in the counting copy be shown, in the
/// workbook, with the response's original characters.
/// </summary>
public sealed record RepairResult(string RepairedText, IReadOnlyList<Finding> Findings, IReadOnlyList<int> SourceIndex);

/// <summary>
/// Produces a counting-only copy of the response: palochka look-alikes inside Cyrillic-dominant
/// words are turned into the real palochka, and Unicode Cf (format, invisible) characters are
/// dropped, everywhere. The original (normalised) text this runs on is never itself mutated in
/// place — callers keep both the input and this result. Every change is recorded as a finding;
/// nothing is repaired silently. The counting copy is for counting only and is never shown to an
/// evaluator (revision 14 §2): what is shown is rebuilt from the original characters through
/// <see cref="RepairResult.SourceIndex"/>.
///
/// Repair runs before sentence/token splitting (see TextAnalyzer) so that a look-alike standing in
/// for punctuation, such as "|" for palochka, does not fragment tokenisation.
///
/// "Cyrillic-dominant" is decided per hyphen-separated part of each whitespace-delimited chunk
/// (revision 14 §3.2): a part with strictly more Cyrillic than Latin letters is in scope for
/// look-alike repair, so "Google-м" (6 Latin vs 1 Cyrillic) and "AI-компьютерым" ("AI" is Latin
/// throughout, "-компьютерым" is its Cyrillic suffix) are left alone while "Iофыгъу" (1 Latin vs
/// 6 Cyrillic) and the "щIэныгъэ" in "сы-ИИ-щIэныгъэ-бот" are repaired. Deciding on the whole chunk
/// had repaired the Latin "I" of "AI-компьютерым" -- a false palochka finding on a legitimate
/// "Latin proper noun + Cyrillic affix" word. Offsets in the returned findings are into
/// <paramref name="normalizedText"/>.
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
            var chunkEnd = chunk.Index + chunk.Length;
            var partStart = chunk.Index;
            for (var i = chunk.Index; i <= chunkEnd; i++)
            {
                if (i != chunkEnd && normalizedText[i] != '-') continue;

                if (i > partStart)
                {
                    var (cyr, lat, _) = ScriptClassifier.CountLetters(normalizedText[partStart..i]);
                    if (cyr > lat)
                    {
                        for (var j = partStart; j < i; j++) isCyrillicDominantAt[j] = true;
                    }
                }
                partStart = i + 1;
            }
        }

        var sb = new StringBuilder(normalizedText.Length);
        var sourceIndex = new List<int>(normalizedText.Length);
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
                sourceIndex.Add(i);
                continue;
            }

            sb.Append(c);
            sourceIndex.Add(i);
        }

        return new RepairResult(sb.ToString(), findings, sourceIndex);
    }
}
