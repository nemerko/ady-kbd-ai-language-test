namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// A language's complete, validated profile: the published alphabet source plus this study's
/// tool-specific settings for it. This is the single object every check and pipeline stage needs.
/// </summary>
public sealed class LanguageProfile
{
    public required AlphabetProfile Alphabet { get; init; }
    public required ToolProfile Tool { get; init; }

    private readonly Dictionary<string, string> _palochkaLookAlikeChars;
    private readonly Dictionary<string, ConfusablePair> _confusableByLatin;

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public LanguageProfile(AlphabetProfile alphabet, ToolProfile tool)
    {
        Alphabet = alphabet;
        Tool = tool;
        _palochkaLookAlikeChars = tool.PalochkaLookAlikes.ToDictionary(l => l.Ch, l => l.Name, StringComparer.Ordinal);
        _confusableByLatin = tool.Confusables.ToDictionary(c => c.Latin, StringComparer.Ordinal);
    }

    public bool IsPalochkaLookAlike(string ch) => _palochkaLookAlikeChars.ContainsKey(ch);

    public bool TryGetConfusable(string latinChar, out ConfusablePair pair) =>
        _confusableByLatin.TryGetValue(latinChar, out pair!);
}
