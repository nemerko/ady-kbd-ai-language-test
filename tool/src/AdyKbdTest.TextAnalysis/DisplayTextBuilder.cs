using System.Text;

namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// Rebuilds each sentence with the response's original characters (revision 14 §3.1). Sentences are
/// found, and everything is counted, on the repaired copy; this walks the per-character source
/// indices the splitter (counting copy -> sentence) and the repairer (normalised text -> counting
/// copy) recorded, so the displayed sentence has exactly the counting sentence's boundaries.
///
/// The two texts can differ in only two ways, and this enforces that rather than assuming it:
/// a repaired look-alike shows its original character, and an invisible (Cf) character the repair
/// dropped is put back where it stood. A dropped character sits in the gap after some kept
/// character; it is shown after that character if the sentence kept it, otherwise (the kept
/// character was a space or marker the splitter discarded) just before the next character the
/// sentence kept.
/// </summary>
internal static class DisplayTextBuilder
{
    public static IReadOnlyList<string> Build(
        string normalizedText,
        string repairedText,
        IReadOnlyList<int> origin,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<TrackedSentence> sentences)
    {
        var n = repairedText.Length;

        var present = new bool[n];
        foreach (var sentence in sentences)
        {
            foreach (var s in sentence.SourceIndex)
            {
                if (s >= 0) present[s] = true;
            }
        }

        string Gap(int p)
        {
            var from = p < 0 ? 0 : origin[p] + 1;
            var to = p + 1 < n ? origin[p + 1] : normalizedText.Length;
            return from < to ? normalizedText[from..to] : "";
        }

        var lead = new string[n];
        var trail = new string[n];
        var pending = new StringBuilder(n > 0 ? Gap(-1) : "");
        var lastPresent = -1;
        for (var p = 0; p < n; p++)
        {
            if (present[p])
            {
                lead[p] = pending.ToString();
                pending.Clear();
                trail[p] = Gap(p);
                lastPresent = p;
            }
            else
            {
                pending.Append(Gap(p));
            }
        }
        if (pending.Length > 0 && lastPresent >= 0) trail[lastPresent] += pending.ToString();

        var replacedOffsets = findings
            .Where(f => f.Code == FindingCodes.PalochkaLookAlike)
            .Select(f => f.Offset)
            .ToHashSet();

        var result = new List<string>(sentences.Count);
        foreach (var sentence in sentences)
        {
            var text = sentence.Span.Text;
            var sb = new StringBuilder(text.Length);
            for (var k = 0; k < text.Length; k++)
            {
                var s = sentence.SourceIndex[k];
                if (s < 0)
                {
                    sb.Append(text[k]);
                    continue;
                }

                var original = normalizedText[origin[s]];
                if (original != repairedText[s] && !replacedOffsets.Contains(origin[s]))
                {
                    throw new InvalidOperationException(
                        $"display text diverged from the counting text at a character that was not a repaired look-alike (counting copy index {s}); refusing to show text that was altered in an unrecorded way.");
                }

                sb.Append(lead[s]);
                sb.Append(original);
                sb.Append(trail[s]);
            }

            var display = sb.ToString();
            var withoutInvisible = display.Count(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format);
            if (withoutInvisible != text.Length)
            {
                throw new InvalidOperationException(
                    "display text and counting text differ in length by more than the invisible characters repair dropped; refusing to continue.");
            }

            result.Add(display);
        }
        return result;
    }
}
