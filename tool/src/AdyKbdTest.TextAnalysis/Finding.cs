namespace AdyKbdTest.TextAnalysis;

public enum FindingSeverity
{
    Low,
    Medium,
    High,
}

/// <summary>The two weights a MIXED_SCRIPT finding can carry; see the coding scheme for the rule.</summary>
public enum MixedScriptWeight
{
    /// <summary>Latin stem + Cyrillic affix, stem capitalised: e.g. "Anthropic-м". Legitimate, low weight.</summary>
    ProperNounAffix,

    /// <summary>Scripts interleaved within a word with no such boundary: e.g. "дüğün". A real error, high weight.</summary>
    Interleaved,
}

public static class FindingCodes
{
    public const string PalochkaLookAlike = "PAL_LOOKALIKE";
    public const string PalochkaCase = "PAL_CASE";
    public const string MixedScript = "MIXED_SCRIPT";
    public const string NonAlphabet = "NON_ALPHABET";
    public const string Confusable = "CONFUSABLE";
    public const string Normalization = "NORMALIZATION";
    public const string Invisible = "INVISIBLE";
}

/// <summary>
/// One character- or token-level finding. Offset/Length are always into the text the finding's
/// stage of the pipeline was scanning; see TextAnalyzer's remarks for which text that is per code.
/// </summary>
public sealed record Finding
{
    public required string Code { get; init; }
    public required FindingSeverity Severity { get; init; }
    public required int Offset { get; init; }
    public required int Length { get; init; }
    public required string Found { get; init; }
    public required string Expected { get; init; }
    public string? Note { get; init; }
    public MixedScriptWeight? Weight { get; init; }
}
