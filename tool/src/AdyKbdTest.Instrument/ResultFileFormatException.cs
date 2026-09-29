namespace AdyKbdTest.Instrument;

/// <summary>Thrown when a raw result file does not match the fixed session-log structure (§5.1).</summary>
public sealed class ResultFileFormatException(string filePath, string message)
    : Exception($"{filePath}: {message}")
{
    public string FilePath { get; } = filePath;
}
