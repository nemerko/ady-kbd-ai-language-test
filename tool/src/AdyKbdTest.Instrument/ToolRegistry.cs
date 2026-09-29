namespace AdyKbdTest.Instrument;

public sealed record ToolInfo(int Number, string Name);

/// <summary>
/// The five tools under test, frozen with the instrument (protocol/instrument.md,
/// "TEST EDİLEN ARAÇLAR"). Not derived from raw files, because round 2 may not exist yet and the
/// numbering must stay stable regardless of which files happen to be present.
/// </summary>
public static class ToolRegistry
{
    public static readonly IReadOnlyList<ToolInfo> Tools =
    [
        new ToolInfo(1, "ChatGPT (OpenAI)"),
        new ToolInfo(2, "Claude (Anthropic)"),
        new ToolInfo(3, "Google Gemini"),
        new ToolInfo(4, "DeepSeek"),
        new ToolInfo(5, "Grok (xAI)"),
    ];

    public static string NameOf(int toolNumber) =>
        Tools.FirstOrDefault(t => t.Number == toolNumber)?.Name ?? $"Tool {toolNumber}";
}
