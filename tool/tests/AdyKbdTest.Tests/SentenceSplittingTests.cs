using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>Revision 1, §7's four new sentence-splitting unit tests, plus the real-data count check.</summary>
[TestClass]
public sealed class SentenceSplittingTests
{
    // 1. A numbered list: markers must not be counted as sentences, must be stripped, and the
    // sentence count must equal the number of items.
    [TestMethod]
    public void NumberedList_MarkersStrippedAndNotCountedAsSentences()
    {
        var result = TextAnalyzer.Analyze(
            "Ары, зэфэшъхьӘфыныгъэ яӏ:\n1. Апэрэм.\n2. Ятӏонэрэм.\n3. ЯщӏӘнэрэм.",
            TestProfiles.Ady);

        Assert.AreEqual(4, result.Sentences.Count); // the intro line + 3 list items
        foreach (var s in result.Sentences)
        {
            Assert.IsFalse(s.Text.StartsWith("1.", StringComparison.Ordinal));
            Assert.IsFalse(s.Text.StartsWith("2.", StringComparison.Ordinal));
            Assert.IsFalse(s.Text.StartsWith("3.", StringComparison.Ordinal));
        }
    }

    // 2. A sentence ending "...дилерим.»" -- the guillemet must stay fused to the sentence, not become its own fragment.
    [TestMethod]
    public void ClosingGuillemetAfterFullStop_StaysFusedToItsSentence()
    {
        var result = TextAnalyzer.Analyze("Ар хъэр аӏо дилерим.»", TestProfiles.Ady);

        Assert.AreEqual(1, result.Sentences.Count);
        Assert.IsTrue(result.Sentences[0].Text.EndsWith("»", StringComparison.Ordinal));
    }

    // 3. A single line break with no blank line must not split.
    [TestMethod]
    public void SingleLineBreakWithoutBlankLine_DoesNotSplit()
    {
        var result = TextAnalyzer.Analyze("Непэ мэфэ\nдах.", TestProfiles.Ady);
        Assert.AreEqual(1, result.Sentences.Count);
    }

    // 4. A block-quoted line: "&gt;" is stripped, the remainder is one sentence.
    [TestMethod]
    public void BlockQuoteMarker_IsStrippedLeavingOneSentence()
    {
        var result = TextAnalyzer.Analyze("> Непэ мэфэ дах.", TestProfiles.Ady);
        Assert.AreEqual(1, result.Sentences.Count);
        Assert.IsFalse(result.Sentences[0].Text.StartsWith(">", StringComparison.Ordinal));
    }

    // Real-data acceptance target: Test 1 -> 235 sentences, Test 2 -> 201, across all 35 responses each.
    [TestMethod]
    public void RealData_Test1And2_MatchDocumentedSentenceCounts()
    {
        var profile = TestProfiles.Ady;

        int CountAll(int test)
        {
            var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
            var total = 0;
            foreach (var r in resolved)
            {
                foreach (var q in r.Selected.Questions)
                {
                    total += TextAnalyzer.Analyze(q.AnswerText, profile).Sentences.Count;
                }
            }
            return total;
        }

        Assert.AreEqual(235, CountAll(1));
        Assert.AreEqual(201, CountAll(2));
    }

    // No letter-less fragment should ever survive as its own "sentence" -- checked directly
    // against the real data, not just inferred from the totals matching.
    [TestMethod]
    public void RealData_NoSentenceIsEntirelyWithoutLetters()
    {
        var profile = TestProfiles.Ady;
        foreach (var test in new[] { 1, 2 })
        {
            var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
            foreach (var r in resolved)
            {
                foreach (var q in r.Selected.Questions)
                {
                    var result = TextAnalyzer.Analyze(q.AnswerText, profile);
                    foreach (var s in result.Sentences)
                    {
                        Assert.IsTrue(s.Text.Any(char.IsLetter), $"letter-less sentence in T{test}-A{r.Selected.Tool}-S{q.Number}: \"{s.Text}\"");
                    }
                }
            }
        }
    }
}
