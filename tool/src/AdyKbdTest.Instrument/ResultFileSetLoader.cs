namespace AdyKbdTest.Instrument;

/// <summary>The attempt selected for measurement for one (test, tool) pair, plus every attempt that was found, for traceability.</summary>
public sealed record ResolvedResultFile(RawResultFile Selected, IReadOnlyList<RawResultFile> AllAttempts);

/// <summary>
/// Loads every result file for one test from a raw round directory and applies the retry rule
/// (§5.1): for each tool, the highest-numbered attempt whose seven answers are all non-empty wins;
/// earlier attempts are kept for traceability but do not enter measurement. If no attempt has all
/// seven answers, the highest-numbered attempt is used anyway (best available) -- this is flagged
/// by "akt verify", not silently accepted as complete.
/// </summary>
public static class ResultFileSetLoader
{
    public static IReadOnlyList<ResolvedResultFile> LoadTest(string rawDir, int test)
    {
        if (!Directory.Exists(rawDir)) return [];

        var pattern = $"Test{test}-AI-Tool*-Result*.txt";
        var files = Directory.GetFiles(rawDir, pattern)
            .Select(ResultFileParser.Parse)
            .Where(f => f.Test == test)
            .ToList();

        var result = new List<ResolvedResultFile>();
        foreach (var group in files.GroupBy(f => f.Tool).OrderBy(g => g.Key))
        {
            var attempts = group.OrderBy(f => f.Attempt).ToList();
            var winner = attempts.Where(f => f.AllAnswersNonEmpty).OrderByDescending(f => f.Attempt).FirstOrDefault()
                ?? attempts.OrderByDescending(f => f.Attempt).First();
            result.Add(new ResolvedResultFile(winner, attempts));
        }
        return result;
    }
}
