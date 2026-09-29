using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Cli;

internal static class AnalysisRunner
{
    public static (LanguageProfile Profile, IReadOnlyList<ResolvedResultFile> ResolvedFiles, Dictionary<ResponseId, AnalyzedResponse> Analyzed)
        Run(string profilesDir, string language, string rawDir, int test)
    {
        var profile = ProfileLoader.Load(profilesDir, language);
        var resolvedFiles = ResultFileSetLoader.LoadTest(rawDir, test);
        if (resolvedFiles.Count == 0)
        {
            throw new InvalidOperationException($"No result files found for test {test} in \"{rawDir}\".");
        }

        var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
        foreach (var resolved in resolvedFiles)
        {
            foreach (var question in resolved.Selected.Questions)
            {
                var id = new ResponseId(test, resolved.Selected.Tool, question.Number);
                analyzed[id] = TextAnalyzer.Analyze(question.AnswerText, profile);
            }
        }

        return (profile, resolvedFiles, analyzed);
    }
}
