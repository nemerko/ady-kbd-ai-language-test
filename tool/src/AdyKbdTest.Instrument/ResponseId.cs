using System.Globalization;

namespace AdyKbdTest.Instrument;

/// <summary>
/// The one key that ties character-level analysis to the marking workbook: T{test}-A{tool}-S{question}.
/// Carries the test number, not the round -- round is only a folder-level grouping (see project docs).
/// </summary>
public readonly record struct ResponseId(int Test, int Tool, int Question) : IComparable<ResponseId>
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"T{Test}-A{Tool}-S{Question}");

    public static bool TryParse(string s, out ResponseId id)
    {
        id = default;
        if (string.IsNullOrEmpty(s)) return false;

        var parts = s.Split('-');
        if (parts.Length != 3) return false;
        if (!TryParseSegment(parts[0], 'T', out var test)) return false;
        if (!TryParseSegment(parts[1], 'A', out var tool)) return false;
        if (!TryParseSegment(parts[2], 'S', out var question)) return false;

        id = new ResponseId(test, tool, question);
        return true;
    }

    public static ResponseId Parse(string s) =>
        TryParse(s, out var id) ? id : throw new FormatException($"\"{s}\" is not a valid response id (expected T{{n}}-A{{n}}-S{{n}}).");

    public int CompareTo(ResponseId other)
    {
        var testCompare = Test.CompareTo(other.Test);
        if (testCompare != 0) return testCompare;
        var toolCompare = Tool.CompareTo(other.Tool);
        if (toolCompare != 0) return toolCompare;
        return Question.CompareTo(other.Question);
    }

    private static bool TryParseSegment(string segment, char prefix, out int value)
    {
        value = 0;
        if (segment.Length < 2 || segment[0] != prefix) return false;
        return int.TryParse(segment.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
