using System.Globalization;
using AdyKbdTest.TextAnalysis.Checks;

namespace AdyKbdTest.TextAnalysis;

/// <summary>
/// Runs the fixed §4.2 pipeline on one raw response: markdown cleanup, NFC normalisation, repair,
/// sentence splitting, tokenisation, checks. Pure: takes text and a profile, returns a result;
/// no file paths, no console output, no Excel. This is what a future web front end would call too.
/// </summary>
public static class TextAnalyzer
{
    public static AnalyzedResponse Analyze(string rawText, LanguageProfile profile)
    {
        var markdown = MarkdownCleaner.Clean(rawText);
        var normalization = TextNormalizer.Normalize(markdown.Text);
        var repair = Repairer.Repair(normalization.NormalizedText, profile);

        var findings = new List<Finding>(normalization.Findings.Count + repair.Findings.Count);
        findings.AddRange(normalization.Findings);
        findings.AddRange(repair.Findings);

        var sentences = new List<AnalyzedSentence>();
        var sentenceIndex = 0;
        foreach (var span in SentenceSplitter.Split(repair.RepairedText))
        {
            var tokenSpans = Tokenizer.Tokenize(span);
            var tokens = new List<AnalyzedToken>(tokenSpans.Count);
            var tokenIndex = 0;
            foreach (var tokenSpan in tokenSpans)
            {
                var isFirst = tokenIndex == 0;
                foreach (var finding in TokenChecks.Run(tokenSpan, isFirst, profile))
                {
                    findings.Add(finding);
                }
                tokens.Add(new AnalyzedToken(tokenSpan.Text, tokenSpan.StartOffset, tokenSpan.Length,
                    ScriptClassifier.ClassifyToken(tokenSpan.Text)));
                tokenIndex++;
            }
            sentences.Add(new AnalyzedSentence(sentenceIndex, span.Text, span.StartOffset, tokens));
            sentenceIndex++;
        }

        var metrics = ComputeMetrics(markdown.HadMarkdown, normalization.WasNfc, normalization.NormalizedText, repair.RepairedText, sentences, findings, profile);

        return new AnalyzedResponse
        {
            RawText = rawText,
            HadMarkdown = markdown.HadMarkdown,
            CleanedText = markdown.Text,
            WasNfc = normalization.WasNfc,
            NormalizedText = normalization.NormalizedText,
            RepairedText = repair.RepairedText,
            Sentences = sentences,
            Findings = findings,
            Metrics = metrics,
        };
    }

    private static ResponseMetrics ComputeMetrics(bool hadMarkdown, bool wasNfc, string normalizedText, string repairedText,
        List<AnalyzedSentence> sentences, List<Finding> findings, LanguageProfile profile)
    {
        var allTokens = sentences.SelectMany(s => s.Tokens).ToList();
        var wordCount = allTokens.Count;
        var sentenceCount = sentences.Count;

        var tokenScriptCounts = Enum.GetValues<ScriptClass>().ToDictionary(s => s, _ => 0);
        foreach (var t in allTokens) tokenScriptCounts[t.Script]++;

        var charScriptCounts = Enum.GetValues<ScriptClass>().ToDictionary(s => s, _ => 0);
        foreach (var c in repairedText) charScriptCounts[ScriptClassifier.ClassifyChar(c)]++;

        var totalWordChars = allTokens.Sum(t => t.Text.Length);
        var averageWordLength = wordCount == 0 ? 0.0 : (double)totalWordChars / wordCount;
        var averageSentenceLength = sentenceCount == 0 ? 0.0 : (double)wordCount / sentenceCount;

        var uniqueWords = allTokens
            .Select(t => t.Text.ToUpper(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);
        var typeTokenRatio = wordCount == 0 ? 0.0 : (double)uniqueWords.Count / wordCount;

        // Counted on the pre-repair (normalised) text deliberately: a look-alike that repair turns
        // into a real palochka must not also be counted here as "already correct" -- it is counted
        // once, as a PAL_LOOKALIKE finding. Counting on the repaired text would double-credit it.
        var palochkaUpper = profile.Alphabet.CharactersOfNote.PalochkaUpper[0];
        var palochkaLower = profile.Alphabet.CharactersOfNote.PalochkaLower[0];
        var correctUpper = normalizedText.Count(c => c == palochkaUpper);
        var correctLower = normalizedText.Count(c => c == palochkaLower);

        var findingCountsByCode = findings
            .GroupBy(f => f.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var mixedScriptByWeight = findings
            .Where(f => f.Code == FindingCodes.MixedScript && f.Weight is not null)
            .GroupBy(f => f.Weight!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var weight in Enum.GetValues<MixedScriptWeight>())
        {
            mixedScriptByWeight.TryAdd(weight, 0);
        }

        var lookalikeCount = findingCountsByCode.GetValueOrDefault(FindingCodes.PalochkaLookAlike, 0);
        var invisibleCount = findingCountsByCode.GetValueOrDefault(FindingCodes.Invisible, 0);

        var latinBreakdown = new Dictionary<char, int>();
        foreach (var token in allTokens)
        {
            var (cyr, lat, _) = ScriptClassifier.CountLetters(token.Text);
            if (cyr <= lat) continue;
            foreach (var c in token.Text)
            {
                if (ScriptClassifier.IsLatinLetter(c))
                {
                    latinBreakdown[c] = latinBreakdown.GetValueOrDefault(c) + 1;
                }
            }
        }

        return new ResponseMetrics
        {
            CharacterCount = repairedText.Length,
            WordCount = wordCount,
            SentenceCount = sentenceCount,
            AverageSentenceLengthWords = averageSentenceLength,
            AverageWordLengthChars = averageWordLength,
            TypeTokenRatio = typeTokenRatio,
            TokenScriptCounts = tokenScriptCounts,
            CharScriptCounts = charScriptCounts,
            CorrectPalochkaUpperCount = correctUpper,
            CorrectPalochkaLowerCount = correctLower,
            LookalikePalochkaCount = lookalikeCount,
            FindingCountsByCode = findingCountsByCode,
            MixedScriptCountsByWeight = mixedScriptByWeight,
            LatinCharacterBreakdownInCyrillicTokens = latinBreakdown,
            IsNfc = wasNfc,
            InvisibleCharacterCount = invisibleCount,
            HadMarkdown = hadMarkdown,
        };
    }
}
