using System.Text.RegularExpressions;

namespace AdyKbdTest.TextAnalysis;

public readonly record struct MarkdownCleanResult(string Text, bool HadMarkdown);

/// <summary>
/// Strips the markdown some tools emit before anything is counted: <c>**bold**</c> pairs and
/// leading <c>#</c> heading marks. Whether the raw answer contained markdown at all -- including
/// list markers and block quotes, which this class detects but deliberately does not strip -- is
/// kept as a per-response flag, because that is itself a behavioural difference between tools.
///
/// List markers (<c>1.</c> <c>-</c> <c>*</c> <c>&gt;</c> <c>•</c>) and residual single asterisks
/// are NOT stripped here under revision 1: <see cref="SentenceSplitter"/> now needs a line-initial
/// list marker to still be present so it can use it as a sentence boundary before discarding it.
/// Stripping it here first (as revision 0 did) would make list-boundary detection impossible for
/// asterisk-led list items, since a global "remove every remaining *" pass run before boundary
/// detection would already have erased the very marker that pass is looking for.
/// </summary>
public static partial class MarkdownCleaner
{
    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldPairRegex();

    [GeneratedRegex(@"^[ \t]*\#{1,6}[ \t]+", RegexOptions.Multiline)]
    private static partial Regex HeadingLineRegex();

    public static MarkdownCleanResult Clean(string text)
    {
        var hadMarkdown = false;

        if (BoldPairRegex().IsMatch(text))
        {
            hadMarkdown = true;
            text = BoldPairRegex().Replace(text, "$1");
        }

        if (HeadingLineRegex().IsMatch(text))
        {
            hadMarkdown = true;
            text = HeadingLineRegex().Replace(text, "");
        }

        if (!hadMarkdown && text.Contains('*'))
        {
            hadMarkdown = true;
        }

        if (!hadMarkdown && text.Split('\n').Any(line => SentenceSplitter.ListMarkerRegex().IsMatch(line)))
        {
            hadMarkdown = true;
        }

        return new MarkdownCleanResult(text, hadMarkdown);
    }
}
