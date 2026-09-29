namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// Thrown when a vendored alphabet source file fails one of the two load-time checks:
/// its SHA-256 does not match the value recorded in the tool profile, or its derived
/// "characters" inventory does not match the single-character entries of "main".
/// </summary>
public sealed class AlphabetValidationException(string message) : Exception(message);
