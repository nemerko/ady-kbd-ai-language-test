namespace AdyKbdTest.Tests;

/// <summary>Locates the repo's tool/ and data/ directories relative to the test assembly, regardless of build configuration.</summary>
internal static class TestPaths
{
    public static string ToolRoot { get; } = FindToolRoot();
    public static string ProfilesDir => Path.Combine(ToolRoot, "profiles");
    public static string RepoRoot => Path.GetFullPath(Path.Combine(ToolRoot, ".."));
    public static string RawRound1Dir => Path.Combine(RepoRoot, "data", "raw", "round1");
    public static string RawRound2Dir => Path.Combine(RepoRoot, "data", "raw", "round2");

    private static string FindToolRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "ady-kbd-ai-language-test.slnx"))) return dir;
            var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            if (parent == dir) break;
            dir = parent;
        }
        throw new InvalidOperationException($"Could not locate ady-kbd-ai-language-test.slnx above {AppContext.BaseDirectory}");
    }
}
