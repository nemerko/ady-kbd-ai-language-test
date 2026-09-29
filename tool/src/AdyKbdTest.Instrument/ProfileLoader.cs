using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Instrument;

/// <summary>
/// The only place in the solution that turns a profiles directory on disk into a
/// <see cref="LanguageProfile"/>. TextAnalysis itself never touches a file path (§2); this class
/// does the File.ReadAllBytes and hands the bytes to TextAnalysis's pure parsers.
/// </summary>
public static class ProfileLoader
{
    public static LanguageProfile Load(string profilesDir, string language)
    {
        var toolProfilePath = Path.Combine(profilesDir, $"tool-profile-{language}.json");
        if (!File.Exists(toolProfilePath))
        {
            throw new FileNotFoundException($"Tool profile not found for language \"{language}\": {toolProfilePath}", toolProfilePath);
        }

        var toolProfile = ToolProfile.Parse(File.ReadAllBytes(toolProfilePath));

        var alphabetPath = Path.Combine(profilesDir, toolProfile.AlphabetSource.File);
        if (!File.Exists(alphabetPath))
        {
            throw new FileNotFoundException(
                $"Vendored alphabet source not found: {alphabetPath}. Not generating a replacement -- see project docs §4.1.",
                alphabetPath);
        }

        var alphabetBytes = File.ReadAllBytes(alphabetPath);
        var alphabetProfile = AlphabetProfile.Parse(alphabetBytes, toolProfile.AlphabetSource.Sha256);

        return new LanguageProfile(alphabetProfile, toolProfile);
    }
}
