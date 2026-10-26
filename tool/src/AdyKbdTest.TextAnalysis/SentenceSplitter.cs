using System.Text;
using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

public sealed record SentenceSpan(string Text, int StartOffset, int Length);

/// <summary>
/// A sentence together with, for each of its characters, the index of that character in the text
/// that was split (-1 for a character the splitter itself made up: the space that joins two lines
/// of one block). <see cref="SourceIndex"/> is what lets the workbook show a sentence found in the
/// counting copy with the response's original characters (revision 14 §3.1).
/// </summary>
public sealed record TrackedSentence(SentenceSpan Span, int[] SourceIndex);

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
///
/// Every stage carries a per-character source index alongside the text (<see cref="SplitTracked"/>);
/// <see cref="Split"/> is the same computation with the indices dropped, so the two cannot disagree
/// about where a sentence begins or ends.
/// </summary>
public static partial class SentenceSplitter
{
    [GeneratedRegex(@"^[ \t]*(?:\d+[.)]|[\-*>•])[ \t]+")]
    internal static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"[.!?…][.!?…""'“”‘’»)\]}]*")]
    private static partial Regex TerminalRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    public static IReadOnlyList<SentenceSpan> Split(string text) =>
        SplitTracked(text).Select(t => t.Span).ToList();

    public static IReadOnlyList<TrackedSentence> SplitTracked(string text)
    {
        var lines = SplitIntoLinesWithOffsets(text);
        var blocks = GroupIntoBlocks(lines);

        var rawSentences = new List<(string Text, int[] Src, int StartOffset)>();
        foreach (var block in blocks)
        {
            if (block.Count == 0) continue;

            var firstLine = block[0];
            var leadWs = firstLine.Text.Length - firstLine.Text.TrimStart(' ', '\t').Length;
            var blockStartOffset = firstLine.Offset + leadWs;

            // Join the trimmed lines with one space, then drop residual asterisks -- recording, for
            // every surviving character, where in the input it came from.
            var chars = new StringBuilder();
            var src = new List<int>();
            for (var li = 0; li < block.Count; li++)
            {
                if (li > 0) { chars.Append(' '); src.Add(-1); }
                var line = block[li];
                var trimmed = line.Text.Trim();
                var lead = line.Text.Length - line.Text.TrimStart().Length;
                for (var k = 0; k < trimmed.Length; k++)
                {
                    if (trimmed[k] == '*') continue;
                    chars.Append(trimmed[k]);
                    src.Add(line.Offset + lead + k);
                }
            }
            var blockText = chars.ToString();
            var blockSrc = src.ToArray();

            var subStart = 0;
            foreach (Match m in TerminalRegex().Matches(blockText))
            {
                var end = m.Index + m.Length;
                if (end > subStart) rawSentences.Add((blockText[subStart..end], blockSrc[subStart..end], blockStartOffset));
                subStart = end;
            }
            if (subStart < blockText.Length) rawSentences.Add((blockText[subStart..], blockSrc[subStart..], blockStartOffset));
        }

        var merged = new List<(string Text, int[] Src, int StartOffset)>();
        foreach (var s in rawSentences)
        {
            var (trimmedText, trimmedSrc) = TrimTracked(s.Text, s.Src);
            if (trimmedText.Length == 0) continue;

            if (!trimmedText.Any(char.IsLetter) && merged.Count > 0)
            {
                var prev = merged[^1];
                merged[^1] = (prev.Text + trimmedText, [.. prev.Src, .. trimmedSrc], prev.StartOffset);
            }
            else
            {
                merged.Add((trimmedText, trimmedSrc, s.StartOffset));
            }
        }

        var result = new List<TrackedSentence>();
        foreach (var (rawText, rawSrc, startOffset) in merged)
        {
            var collapsedChars = new StringBuilder();
            var collapsedSrc = new List<int>();
            var pos = 0;
            foreach (Match m in WhitespaceRunRegex().Matches(rawText))
            {
                for (var i = pos; i < m.Index; i++) { collapsedChars.Append(rawText[i]); collapsedSrc.Add(rawSrc[i]); }
                collapsedChars.Append(' ');
                collapsedSrc.Add(rawSrc[m.Index]);
                pos = m.Index + m.Length;
            }
            for (var i = pos; i < rawText.Length; i++) { collapsedChars.Append(rawText[i]); collapsedSrc.Add(rawSrc[i]); }

            var (collapsed, collapsedSrcTrimmed) = TrimTracked(collapsedChars.ToString(), collapsedSrc.ToArray());
            if (collapsed.Length == 0) continue;
            result.Add(new TrackedSentence(new SentenceSpan(collapsed, startOffset, collapsed.Length), collapsedSrcTrimmed));
        }
        return result;
    }

    private static (string Text, int[] Src) TrimTracked(string text, int[] src)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        return (text[start..end], src[start..end]);
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
}
