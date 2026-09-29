using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>
/// §7's end-to-end check, run on the real data/raw/round1 files: Claude (tool 2), excluding
/// question 4, wrote the palochka as Latin capital I 52 times in Test 1 (38 correct, 42%) and zero
/// times in Test 2 (67 correct, 100%).
/// </summary>
[TestClass]
public sealed class EndToEndTests
{
    private static (int Correct, int Lookalike) AnalyzeTool2ExcludingQuestion4(int test)
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
        var tool2 = resolved.Single(r => r.Selected.Tool == 2).Selected;

        var correct = 0;
        var lookalike = 0;
        foreach (var question in tool2.Questions.Where(q => q.Number != 4))
        {
            var analyzed = TextAnalyzer.Analyze(question.AnswerText, profile);
            correct += analyzed.Metrics.CorrectPalochkaUpperCount + analyzed.Metrics.CorrectPalochkaLowerCount;
            lookalike += analyzed.Metrics.LookalikePalochkaCount;
        }
        return (correct, lookalike);
    }

    [TestMethod]
    public void Test1_Claude_ExcludingQuestion4_Has38CorrectAnd52LatinI()
    {
        var (correct, lookalike) = AnalyzeTool2ExcludingQuestion4(1);
        Assert.AreEqual(38, correct);
        Assert.AreEqual(52, lookalike);
    }

    [TestMethod]
    public void Test2_Claude_ExcludingQuestion4_Has67CorrectAndZeroLatinI()
    {
        var (correct, lookalike) = AnalyzeTool2ExcludingQuestion4(2);
        Assert.AreEqual(67, correct);
        Assert.AreEqual(0, lookalike);
    }

    [TestMethod]
    public void Gemini_GoogleSuffix_IsNeverFlaggedAsPalochkaError()
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 1);
        var tool3 = resolved.Single(r => r.Selected.Tool == 3).Selected;

        foreach (var question in tool3.Questions)
        {
            var analyzed = TextAnalyzer.Analyze(question.AnswerText, profile);
            Assert.AreEqual(0, analyzed.Metrics.LookalikePalochkaCount,
                $"S{question.Number} should not flag \"Google-м\" as a palochka look-alike error.");
        }
    }
}
