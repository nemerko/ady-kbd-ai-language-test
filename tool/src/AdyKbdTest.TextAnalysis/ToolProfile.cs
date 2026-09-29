using System.Text.Json.Serialization;

namespace AdyKbdTest.TextAnalysis;

/// <summary>A Latin, Cyrillic or symbol character that can be mistaken for a palochka in running text.</summary>
public sealed record PalochkaLookAlike
{
    [JsonPropertyName("ch")]
    public required string Ch { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

/// <summary>One Cyrillic/Latin homoglyph pair from the tool profile's "confusables" table.</summary>
public sealed record ConfusablePair
{
    public required string Cyrillic { get; init; }
    public required string Latin { get; init; }
}

/// <summary>Provenance and integrity data for the vendored alphabet source this tool profile points at.</summary>
public sealed record AlphabetSourceInfo
{
    [JsonPropertyName("citation")]
    public required string Citation { get; init; }

    [JsonPropertyName("conceptDoi")]
    public string? ConceptDoi { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}

/// <summary>
/// Study-specific settings that do not belong in the published alphabet source: look-alike and
/// confusable tables, normalisation target, contact languages. Never modifies the vendored source.
/// </summary>
public sealed class ToolProfile
{
    public required string Language { get; init; }
    public required AlphabetSourceInfo AlphabetSource { get; init; }
    public required string NormalizationForm { get; init; }
    public required IReadOnlyList<PalochkaLookAlike> PalochkaLookAlikes { get; init; }
    public required IReadOnlyList<ConfusablePair> Confusables { get; init; }
    public required IReadOnlyList<string> ContactLanguages { get; init; }

    /// <summary>Pure parse from raw bytes; no file I/O.</summary>
    public static ToolProfile Parse(byte[] jsonBytes)
    {
        var dto = System.Text.Json.JsonSerializer.Deserialize<ToolProfileDto>(jsonBytes)
            ?? throw new AlphabetValidationException("Tool profile JSON deserialized to null.");

        var alphabetSource = dto.AlphabetSource
            ?? throw new AlphabetValidationException("Tool profile is missing \"alphabetSource\".");

        var confusables = (dto.Confusables ?? [])
            .Where(pair => pair.Count == 2)
            .Select(pair => new ConfusablePair { Cyrillic = pair[0], Latin = pair[1] })
            .ToList();

        return new ToolProfile
        {
            Language = dto.Language ?? "",
            AlphabetSource = alphabetSource,
            NormalizationForm = dto.NormalizationForm ?? "NFC",
            PalochkaLookAlikes = dto.PalochkaLookAlikes ?? [],
            Confusables = confusables,
            ContactLanguages = dto.ContactLanguages ?? [],
        };
    }

    private sealed class ToolProfileDto
    {
        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("alphabetSource")]
        public AlphabetSourceInfo? AlphabetSource { get; init; }

        [JsonPropertyName("normalizationForm")]
        public string? NormalizationForm { get; init; }

        [JsonPropertyName("palochkaLookAlikes")]
        public List<PalochkaLookAlike>? PalochkaLookAlikes { get; init; }

        [JsonPropertyName("confusables")]
        public List<List<string>>? Confusables { get; init; }

        [JsonPropertyName("contactLanguages")]
        public List<string>? ContactLanguages { get; init; }
    }
}
