namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// Plain Levenshtein edit distance, hand-written (no third-party package), and a normalised
/// similarity derived from it. Comparison is ordinal, character by character; culture never enters
/// into it. Used for the copying probe (question 5) and for cross-test stability comparisons.
/// </summary>
public static class LevenshteinSimilarity
{
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var n = a.Length;
        var m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var previous = new int[m + 1];
        var current = new int[m + 1];
        for (var j = 0; j <= m; j++) previous[j] = j;

        for (var i = 1; i <= n; i++)
        {
            current[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[m];
    }

    /// <summary>Normalised similarity in [0, 1]; 1 means identical, 0 means completely different. Two empty strings are identical (1.0).</summary>
    public static double NormalizedSimilarity(string a, string b)
    {
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        var distance = Distance(a, b);
        return 1.0 - (double)distance / maxLen;
    }
}
