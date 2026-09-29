using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace AdyKbdTest.TextAnalysis;

/// <summary>One entry of the alphabet's "main" or "auxiliary" list: a single letter, digraph or trigraph.</summary>
public sealed record AlphabetLetter
{
    [JsonPropertyName("n")]
    public int N { get; init; }

    [JsonPropertyName("alphabet_index")]
    public int? AlphabetIndex { get; init; }

    [JsonPropertyName("cyrl_lower")]
    public required string CyrlLower { get; init; }

    [JsonPropertyName("cyrl_title")]
    public required string CyrlTitle { get; init; }

    [JsonPropertyName("cyrl_upper")]
    public required string CyrlUpper { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("dialect")]
    public string? Dialect { get; init; }
}

/// <summary>Palochka code points, kept apart from ordinary confusables because they are the study's central check.</summary>
public sealed record CharactersOfNote
{
    [JsonPropertyName("palochka_upper")]
    public required string PalochkaUpper { get; init; }

    [JsonPropertyName("palochka_lower")]
    public required string PalochkaLower { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

/// <summary>The single-character inventory derived from "main", used to test alphabet membership.</summary>
public sealed record CharacterInventory
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("lower")]
    public required string Lower { get; init; }

    [JsonPropertyName("upper")]
    public required string Upper { get; init; }
}

/// <summary>
/// A published, DOI-archived Cyrillic alphabet source (Nemlioğlu 2026), vendored as-is.
/// This type never invents or repairs alphabet data; it only parses and validates it.
/// </summary>
public sealed class AlphabetProfile
{
    public string Language { get; }
    public IReadOnlyList<AlphabetLetter> Main { get; }
    public IReadOnlyList<AlphabetLetter> Auxiliary { get; }
    public CharacterInventory Characters { get; }
    public CharactersOfNote CharactersOfNote { get; }

    /// <summary>
    /// "main" sorted by the length of cyrl_lower, longest first, as required by the source's own
    /// matching rule: entries must be tried longest-first in running text or multi-character
    /// letters (e.g. "кӏь") get split by their own shorter prefixes ("кӏ", "к").
    /// </summary>
    public IReadOnlyList<AlphabetLetter> MatchingOrder { get; }

    private readonly HashSet<string> _lowerCharSet;
    private readonly HashSet<string> _upperCharSet;

    private AlphabetProfile(string language, IReadOnlyList<AlphabetLetter> main, IReadOnlyList<AlphabetLetter> auxiliary,
        CharacterInventory characters, CharactersOfNote charactersOfNote)
    {
        Language = language;
        Main = main;
        Auxiliary = auxiliary;
        Characters = characters;
        CharactersOfNote = charactersOfNote;

        MatchingOrder = [.. main.OrderByDescending(l => l.CyrlLower.Length)];

        _lowerCharSet = new HashSet<string>(EnumerateChars(characters.Lower), StringComparer.Ordinal);
        _upperCharSet = new HashSet<string>(EnumerateChars(characters.Upper), StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateChars(string s)
    {
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext())
        {
            yield return (string)e.Current;
        }
    }

    /// <summary>True if the (single-codepoint) character belongs to this alphabet's lower or upper inventory.</summary>
    public bool IsAlphabetCharacter(string ch) => _lowerCharSet.Contains(ch) || _upperCharSet.Contains(ch);

    public bool IsPalochka(string ch) =>
        string.Equals(ch, CharactersOfNote.PalochkaUpper, StringComparison.Ordinal) ||
        string.Equals(ch, CharactersOfNote.PalochkaLower, StringComparison.Ordinal);

    /// <summary>
    /// Parses a vendored alphabet JSON file from raw bytes and validates it: the SHA-256 of
    /// <paramref name="jsonBytes"/> must equal <paramref name="expectedSha256Hex"/>, and the
    /// "characters" inventory must equal the one derivable from the single-character "main" entries.
    /// Pure: takes bytes, does no file I/O of its own.
    /// </summary>
    public static AlphabetProfile Parse(byte[] jsonBytes, string expectedSha256Hex)
    {
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(jsonBytes));
        if (!string.Equals(actualHash, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new AlphabetValidationException(
                $"Alphabet source SHA-256 mismatch: expected {expectedSha256Hex}, computed {actualHash}.");
        }

        var dto = System.Text.Json.JsonSerializer.Deserialize<AlphabetProfileDto>(jsonBytes)
            ?? throw new AlphabetValidationException("Alphabet source JSON deserialized to null.");

        var main = dto.Main ?? throw new AlphabetValidationException("Alphabet source is missing \"main\".");
        var auxiliary = dto.Auxiliary ?? [];
        var characters = dto.Characters ?? throw new AlphabetValidationException("Alphabet source is missing \"characters\".");
        var charactersOfNote = dto.CharactersOfNote ?? throw new AlphabetValidationException("Alphabet source is missing \"characters_of_note\".");

        var derivedLower = string.Concat(main.Where(l => l.Type == "single").OrderBy(l => l.AlphabetIndex).Select(l => l.CyrlLower));
        var derivedUpper = string.Concat(main.Where(l => l.Type == "single").OrderBy(l => l.AlphabetIndex).Select(l => l.CyrlUpper));

        if (!string.Equals(derivedLower, characters.Lower, StringComparison.Ordinal))
        {
            throw new AlphabetValidationException(
                $"\"characters.lower\" does not match the single-character entries of \"main\": expected \"{derivedLower}\", found \"{characters.Lower}\".");
        }
        if (!string.Equals(derivedUpper, characters.Upper, StringComparison.Ordinal))
        {
            throw new AlphabetValidationException(
                $"\"characters.upper\" does not match the single-character entries of \"main\": expected \"{derivedUpper}\", found \"{characters.Upper}\".");
        }

        return new AlphabetProfile(dto.Language ?? "", main, auxiliary, characters, charactersOfNote);
    }

    private sealed class AlphabetProfileDto
    {
        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("main")]
        public List<AlphabetLetter>? Main { get; init; }

        [JsonPropertyName("auxiliary")]
        public List<AlphabetLetter>? Auxiliary { get; init; }

        [JsonPropertyName("characters")]
        public CharacterInventory? Characters { get; init; }

        [JsonPropertyName("characters_of_note")]
        public CharactersOfNote? CharactersOfNote { get; init; }
    }
}
