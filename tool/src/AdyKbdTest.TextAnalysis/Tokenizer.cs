namespace AdyKbdTest.TextAnalysis;

public sealed record TokenSpan(string Text, int StartOffset, int Length);

/// <summary>
/// Splits a sentence into tokens on whitespace, then trims leading and trailing punctuation from
/// each token. Internal hyphens and apostrophes are never touched, so "Anthropic-м" stays one
/// token. A token that trims down to nothing (a stray run of punctuation between two words) is
/// dropped rather than counted as an empty word -- and so, since revision 9 §3, is a token that
/// survives trimming but still contains no Unicode letter at all (an emoji, a lone dash the trim
/// list does not happen to cover, a bare punctuation mark): the same "no letter, not a word" rule
/// <see cref="SentenceSplitter"/> already applies to a letterless sentence fragment, applied
/// consistently at the word level. A letter <em>inside</em> an otherwise-word token, or a mark
/// attached to one ("шӏу!😊"), does not change anything -- that token already has a letter and
/// stays a word regardless of what else it carries.
/// </summary>
public static class Tokenizer
{
    // « » " " „ “ ” ' ' ' . , ; : ! ? ( ) [ ] { } … — – * _
    private static readonly char[] TrimChars =
    [
        '«', '»', '"', '“', '”', '„',
        '\'', '‘', '’',
        '.', ',', ';', ':', '!', '?',
        '(', ')', '[', ']', '{', '}',
        '…', '—', '–',
        '*', '_',
    ];

    public static IReadOnlyList<TokenSpan> Tokenize(SentenceSpan sentence)
    {
        var result = new List<TokenSpan>();
        var text = sentence.Text;
        var i = 0;

        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length) break;

            var wordStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            var wordEnd = i;

            var trimStart = wordStart;
            while (trimStart < wordEnd && Array.IndexOf(TrimChars, text[trimStart]) >= 0) trimStart++;

            var trimEnd = wordEnd;
            while (trimEnd > trimStart && Array.IndexOf(TrimChars, text[trimEnd - 1]) >= 0) trimEnd--;

            if (trimEnd <= trimStart) continue;

            var tokenText = text[trimStart..trimEnd];
            if (!tokenText.Any(char.IsLetter)) continue;

            var globalStart = sentence.StartOffset + trimStart;
            result.Add(new TokenSpan(tokenText, globalStart, trimEnd - trimStart));
        }

        return result;
    }
}
