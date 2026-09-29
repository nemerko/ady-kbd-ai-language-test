namespace AdyKbdTest.TextAnalysis;

public sealed record AnalyzedToken(string Text, int StartOffset, int Length, ScriptClass Script);

public sealed record AnalyzedSentence(int Index, string Text, int StartOffset, IReadOnlyList<AnalyzedToken> Tokens);

/// <summary>
/// Per-response measurements, as required by the coding scheme's §4.4. Character/word/sentence
/// counts and the Latin-in-Cyrillic breakdown are computed on the repaired (counting) text; the
/// NFC and markdown flags describe the response as it originally arrived.
/// </summary>
public sealed record ResponseMetrics
{
    public required int CharacterCount { get; init; }
    public required int WordCount { get; init; }
    public required int SentenceCount { get; init; }
    public required double AverageSentenceLengthWords { get; init; }
    public required double AverageWordLengthChars { get; init; }
    public required double TypeTokenRatio { get; init; }
    public required IReadOnlyDictionary<ScriptClass, int> TokenScriptCounts { get; init; }
    public required IReadOnlyDictionary<ScriptClass, int> CharScriptCounts { get; init; }
    public required int CorrectPalochkaUpperCount { get; init; }
    public required int CorrectPalochkaLowerCount { get; init; }
    public required int LookalikePalochkaCount { get; init; }
    public required IReadOnlyDictionary<string, int> FindingCountsByCode { get; init; }
    public required IReadOnlyDictionary<MixedScriptWeight, int> MixedScriptCountsByWeight { get; init; }
    public required IReadOnlyDictionary<char, int> LatinCharacterBreakdownInCyrillicTokens { get; init; }
    public required bool IsNfc { get; init; }
    public required int InvisibleCharacterCount { get; init; }
    public required bool HadMarkdown { get; init; }
}

/// <summary>
/// The full result of running the pipeline (§4.2) on one response: every intermediate text, every
/// finding, the sentence/token breakdown and the response-level metrics.
/// </summary>
public sealed record AnalyzedResponse
{
    public required string RawText { get; init; }
    public required bool HadMarkdown { get; init; }
    public required string CleanedText { get; init; }
    public required bool WasNfc { get; init; }
    public required string NormalizedText { get; init; }
    public required string RepairedText { get; init; }
    public required IReadOnlyList<AnalyzedSentence> Sentences { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
    public required ResponseMetrics Metrics { get; init; }
}
