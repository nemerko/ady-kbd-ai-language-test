using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Instrument;

/// <summary>One response's analysis, flattened for TSV/JSON export to data/analysis. This is what "akt analyze" writes and "akt report" reads back for cross-test comparisons.</summary>
public sealed record AnalysisRecord
{
    public required string Id { get; init; }
    public required int Test { get; init; }
    public required int Tool { get; init; }
    public required int Question { get; init; }
    public required string RepairedText { get; init; }
    public required bool HadMarkdown { get; init; }
    public required bool IsNfc { get; init; }
    public required int CharacterCount { get; init; }
    public required int WordCount { get; init; }
    public required int SentenceCount { get; init; }
    public required double AvgSentenceLengthWords { get; init; }
    public required double AvgWordLengthChars { get; init; }
    public required double TypeTokenRatio { get; init; }
    public required int CorrectPalochkaUpper { get; init; }
    public required int CorrectPalochkaLower { get; init; }
    public required int LookalikePalochka { get; init; }
    public required int InvisibleCount { get; init; }
    public required IReadOnlyDictionary<string, int> FindingCountsByCode { get; init; }
    public required IReadOnlyDictionary<string, int> MixedScriptByWeight { get; init; }
    public required IReadOnlyDictionary<string, int> LatinInCyrillicBreakdown { get; init; }
    public required IReadOnlyDictionary<string, int> TokenScriptCounts { get; init; }
    public required IReadOnlyDictionary<string, int> CharScriptCounts { get; init; }

    /// <summary>The mechanical copying-probe result (§6), set only for question 5.</summary>
    public string? CopyProbe { get; init; }

    public static AnalysisRecord From(ResponseId id, AnalyzedResponse analyzed)
    {
        var m = analyzed.Metrics;
        return new AnalysisRecord
        {
            CopyProbe = id.Question == 5 ? CopyProbeDetector.Detect(analyzed.RepairedText).ToString() : null,
            Id = id.ToString(),
            Test = id.Test,
            Tool = id.Tool,
            Question = id.Question,
            RepairedText = analyzed.RepairedText,
            HadMarkdown = m.HadMarkdown,
            IsNfc = m.IsNfc,
            CharacterCount = m.CharacterCount,
            WordCount = m.WordCount,
            SentenceCount = m.SentenceCount,
            AvgSentenceLengthWords = m.AverageSentenceLengthWords,
            AvgWordLengthChars = m.AverageWordLengthChars,
            TypeTokenRatio = m.TypeTokenRatio,
            CorrectPalochkaUpper = m.CorrectPalochkaUpperCount,
            CorrectPalochkaLower = m.CorrectPalochkaLowerCount,
            LookalikePalochka = m.LookalikePalochkaCount,
            InvisibleCount = m.InvisibleCharacterCount,
            FindingCountsByCode = m.FindingCountsByCode,
            MixedScriptByWeight = m.MixedScriptCountsByWeight.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            LatinInCyrillicBreakdown = m.LatinCharacterBreakdownInCyrillicTokens.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            TokenScriptCounts = m.TokenScriptCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            CharScriptCounts = m.CharScriptCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        };
    }
}

public static class AnalysisExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void WriteJson(string path, IReadOnlyList<AnalysisRecord> records)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(records, JsonOptions), System.Text.Encoding.UTF8);
    }

    public static List<AnalysisRecord> ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<AnalysisRecord>>(json, JsonOptions) ?? [];
    }

    public static void WriteTsv(string path, IReadOnlyList<AnalysisRecord> records)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new System.Text.StringBuilder();
        sb.Append(string.Join('\t', "id", "test", "tool", "question", "character_count", "word_count",
            "sentence_count", "avg_sentence_length_words", "avg_word_length_chars", "type_token_ratio",
            "correct_palochka_upper", "correct_palochka_lower", "lookalike_palochka", "invisible_count",
            "had_markdown", "is_nfc", "copy_probe"));
        sb.Append('\n');

        foreach (var r in records.OrderBy(r => r.Test).ThenBy(r => r.Tool).ThenBy(r => r.Question))
        {
            sb.Append(string.Join('\t',
                r.Id, Inv(r.Test), Inv(r.Tool), Inv(r.Question), Inv(r.CharacterCount), Inv(r.WordCount),
                Inv(r.SentenceCount), Inv(r.AvgSentenceLengthWords), Inv(r.AvgWordLengthChars), Inv(r.TypeTokenRatio),
                Inv(r.CorrectPalochkaUpper), Inv(r.CorrectPalochkaLower), Inv(r.LookalikePalochka), Inv(r.InvisibleCount),
                r.HadMarkdown ? "1" : "0", r.IsNfc ? "1" : "0", r.CopyProbe ?? ""));
            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
    }

    private static string Inv(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static string Inv(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
