using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

public sealed record SentenceSpan(string Text, int StartOffset, int Length);

/// <summary>
/// Splits the repaired (counting) text into sentences under the revised rule (revision 1, which
/// replaces §4.2's original sentence-splitting rule entirely):
///
/// 1. A line-initial list marker (<c>1.</c> <c>1)</c> <c>-</c> <c>*</c> <c>&gt;</c> <c>•</c>,
///    followed by whitespace) starts a new block and the marker itself is discarded.
/// 2. A blank line also starts a new block (paragraph break). A single line break alone is not a
///    boundary -- lines within one block are joined with a single space.
/// 3. Within each block's text, a run starting with <c>. ! ? …</c> is a sentence boundary; any
///    further terminal punctuation or closing quote/bracket immediately following
///    (<c>" ' " ' » ) ] }</c>) is absorbed into the same, ending sentence rather than starting a
///    new (typically empty) one -- this is what keeps a lone trailing <c>»</c> from becoming its
///    own "sentence".
/// 4. A resulting fragment with no letters at all is glued onto the previous sentence, as a final
///    safety net for anything the earlier rules still let through as its own fragment.
/// 5. Internal whitespace is collapsed to one space.
///
/// Offsets are block-level: every sentence produced from the same block (the common case is one
/// sentence per block, but a multi-sentence paragraph produces several) carries that block's start
/// offset, not each sentence's own. No test or caller needs finer-grained offsets than that today;
/// see tool/README.md.
/// </summary>
public static partial class SentenceSplitter
{
    [GeneratedRegex(@"^[ \t]*(?:\d+[.)]|[\-*>•])[ \t]+")]
    internal static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"[.!?…][.!?…""'“”‘’»)\]}]*")]
    private static partial Regex TerminalRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    public static IReadOnlyList<SentenceSpan> Split(string text)
    {
        var lines = SplitIntoLinesWithOffsets(text);
        var blocks = GroupIntoBlocks(lines);

        var rawSentences = new List<(string Text, int StartOffset)>();
        foreach (var block in blocks)
        {
            if (block.Count == 0) continue;

            var firstLine = block[0];
            var leadWs = firstLine.Text.Length - firstLine.Text.TrimStart(' ', '\t').Length;
            var blockStartOffset = firstLine.Offset + leadWs;

            var joined = string.Join(' ', block.Select(l => l.Text.Trim()));
            var blockText = StripResidualAsterisks(joined);

            var subStart = 0;
            foreach (Match m in TerminalRegex().Matches(blockText))
            {
                var end = m.Index + m.Length;
                if (end > subStart) rawSentences.Add((blockText[subStart..end], blockStartOffset));
                subStart = end;
            }
            if (subStart < blockText.Length) rawSentences.Add((blockText[subStart..], blockStartOffset));
        }

        var merged = new List<(string Text, int StartOffset)>();
        foreach (var s in rawSentences)
        {
            var trimmed = s.Text.Trim();
            if (trimmed.Length == 0) continue;

            if (!trimmed.Any(char.IsLetter) && merged.Count > 0)
            {
                var prev = merged[^1];
                merged[^1] = (prev.Text + trimmed, prev.StartOffset);
            }
            else
            {
                merged.Add((trimmed, s.StartOffset));
            }
        }

        var result = new List<SentenceSpan>();
        foreach (var (rawText, startOffset) in merged)
        {
            var collapsed = WhitespaceRunRegex().Replace(rawText, " ").Trim();
            if (collapsed.Length == 0) continue;
            result.Add(new SentenceSpan(collapsed, startOffset, collapsed.Length));
        }
        return result;
    }

    private static List<(string Text, int Offset)> SplitIntoLinesWithOffsets(string text)
    {
        var lines = new List<(string Text, int Offset)>();
        var pos = 0;
        foreach (var part in text.Split('\n'))
        {
            lines.Add((part, pos));
            pos += part.Length + 1;
        }
        return lines;
    }

    private static List<List<(string Text, int Offset)>> GroupIntoBlocks(List<(string Text, int Offset)> lines)
    {
        var blocks = new List<List<(string Text, int Offset)>>();
        var current = new List<(string Text, int Offset)>();

        foreach (var line in lines)
        {
            var trimmedLeading = line.Text.TrimStart(' ', '\t');
            if (trimmedLeading.Length == 0)
            {
                if (current.Count > 0) { blocks.Add(current); current = []; }
                continue;
            }

            var marker = ListMarkerRegex().Match(line.Text);
            if (marker.Success)
            {
                if (current.Count > 0) { blocks.Add(current); current = []; }
                current.Add((line.Text[marker.Length..], line.Offset + marker.Length));
            }
            else
            {
                current.Add(line);
            }
        }
        if (current.Count > 0) blocks.Add(current);

        return blocks;
    }

    /// <summary>
    /// Removes any "*" left after list-marker stripping -- residual bold/italic markup (the
    /// leading "**"/"*" pairs matching §1's markdown cleanup were already removed by
    /// <see cref="MarkdownCleaner"/> before this runs; this only catches stray single asterisks,
    /// e.g. an italic *word* that was not at a line start and so was never a list marker).
    /// </summary>
    private static string StripResidualAsterisks(string s) => s.Replace("*", "", StringComparison.Ordinal);
}
