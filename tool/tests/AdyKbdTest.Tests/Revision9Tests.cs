using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 9: a whitespace-separated item with no Unicode letter in it (an emoji, a lone dash, a
/// bare digit run) is no longer counted as a word -- the same "no letter, not a word" rule
/// <see cref="SentenceSplitter"/> already applied to letterless sentence fragments (revision 1
/// §5 rule 5), now applied consistently at the word level too.
/// </summary>
[TestClass]
public sealed class Revision9Tests
{
    private static IReadOnlyList<string> TokenTexts(string sentenceText)
    {
        var span = new SentenceSpan(sentenceText, 0, sentenceText.Length);
        return Tokenizer.Tokenize(span).Select(t => t.Text).ToList();
    }

    // §1's own motivating example: the tool counted this as 3 words, the correct count is 2.
    [TestMethod]
    public void EmojiToken_IsNotCountedAsAWord()
    {
        CollectionAssert.AreEqual(new[] { "Ар", "уахэт" }, TokenTexts("😊 Ар уахэт?").ToList());
    }

    [DataTestMethod]
    [DataRow("Тхьэ — жьы", DisplayName = "standalone em dash")]
    [DataRow("Тхьэ – жьы", DisplayName = "standalone en dash")]
    [DataRow("Тхьэ . жьы", DisplayName = "standalone period")]
    [DataRow("Тхьэ 13.09.2026 жьы", DisplayName = "digit-only item, including one with internal punctuation")]
    public void LetterlessStandaloneItem_IsDroppedFromTheTokenCount(string sentenceText)
    {
        CollectionAssert.AreEqual(new[] { "Тхьэ", "жьы" }, TokenTexts(sentenceText).ToList());
    }

    // §3: a letter *inside* an otherwise-word token changes nothing -- internal punctuation and an
    // attached emoji do not disqualify a token that already has a letter.
    [TestMethod]
    public void TokenWithInternalPunctuationAndEmoji_IsStillCountedAsOneWord_WhenItHasALetter()
    {
        CollectionAssert.AreEqual(new[] { "шӏу!😊" }, TokenTexts("шӏу!😊").ToList());
    }

    // §5 acceptance: the exact real-data sentence from §1, run through the whole pipeline (not
    // just the tokenizer in isolation), now reports a word count of 2, not 3.
    [TestMethod]
    public void RealData_EmojiSentence_WordCountIsTwo()
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 1);
        var tool1 = resolved.Single(r => r.Selected.Tool == 1);
        var q1 = tool1.Selected.Questions.Single(q => q.Number == 1);
        var analyzed = TextAnalyzer.Analyze(q1.AnswerText, profile);

        var sentence = analyzed.Sentences.Single(s => s.Text.Contains("Ар уахэт", StringComparison.Ordinal));
        Assert.AreEqual(2, sentence.Tokens.Count);
    }

    // §5 acceptance: the real Test 1 data's total word count across all 235 sentences drops to
    // 1544 -- every letterless item (the emoji, the standalone "/") is now excluded.
    [TestMethod]
    public void RealData_Test1TotalWordCount_Is1544()
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 1);
        var total = 0;
        foreach (var r in resolved)
        {
            foreach (var q in r.Selected.Questions)
            {
                var analyzed = TextAnalyzer.Analyze(q.AnswerText, profile);
                total += analyzed.Sentences.Sum(s => s.Tokens.Count);
            }
        }

        Assert.AreEqual(1544, total);
    }
}
