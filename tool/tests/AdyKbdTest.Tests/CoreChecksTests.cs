using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>Tests 1-6 and 9-11 of the project docs' acceptance list (§7).</summary>
[TestClass]
public sealed class CoreChecksTests
{
    // 1. "Iофыгъу" (Latin capital I) -> PAL_LOOKALIKE, found U+0049, expected U+04C0.
    [TestMethod]
    public void Test1_LatinCapitalI_ProducesPalochkaLookalikeFinding()
    {
        var result = TextAnalyzer.Analyze("Iофыгъу", TestProfiles.Ady);
        var finding = result.Findings.Single(f => f.Code == FindingCodes.PalochkaLookAlike);
        Assert.AreEqual('I', finding.Found[0]);
        Assert.AreEqual(0x0049, (int)finding.Found[0]);
        Assert.AreEqual(0x04C0, (int)finding.Expected[0]);
    }

    // 2. "Ӏофыгъу" (real palochka U+04C0) -> no finding.
    [TestMethod]
    public void Test2_RealPalochka_ProducesNoFinding()
    {
        var result = TextAnalyzer.Analyze("Ӏофыгъу", TestProfiles.Ady);
        Assert.AreEqual(0, result.Findings.Count(f => f.Code == FindingCodes.PalochkaLookAlike));
    }

    // 3. "Google-м" -> no PAL_LOOKALIKE (the "l" is part of the Latin proper noun); MIXED_SCRIPT / ProperNounAffix instead.
    [TestMethod]
    public void Test3_GoogleSuffix_NoLookalikeButProperNounAffix()
    {
        var result = TextAnalyzer.Analyze("Google-м", TestProfiles.Ady);
        Assert.AreEqual(0, result.Findings.Count(f => f.Code == FindingCodes.PalochkaLookAlike));
        var mixed = result.Findings.Single(f => f.Code == FindingCodes.MixedScript);
        Assert.AreEqual(MixedScriptWeight.ProperNounAffix, mixed.Weight);
    }

    // 4. "Anthropic-м", "ChatGPT-щ" -> MIXED_SCRIPT / ProperNounAffix, no palochka finding.
    [TestMethod]
    [DataRow("Anthropic-м")]
    [DataRow("ChatGPT-щ")]
    public void Test4_LatinStemCyrillicAffix_IsProperNounAffix(string word)
    {
        var result = TextAnalyzer.Analyze(word, TestProfiles.Ady);
        Assert.AreEqual(0, result.Findings.Count(f => f.Code == FindingCodes.PalochkaLookAlike));
        var mixed = result.Findings.Single(f => f.Code == FindingCodes.MixedScript);
        Assert.AreEqual(MixedScriptWeight.ProperNounAffix, mixed.Weight);
    }

    // 5. "дüğün" -> MIXED_SCRIPT / Interleaved, high weight.
    [TestMethod]
    public void Test5_InterleavedScripts_IsInterleavedHighWeight()
    {
        var result = TextAnalyzer.Analyze("дüğün", TestProfiles.Ady);
        var mixed = result.Findings.Single(f => f.Code == FindingCodes.MixedScript);
        Assert.AreEqual(MixedScriptWeight.Interleaved, mixed.Weight);
        Assert.AreEqual(FindingSeverity.High, mixed.Severity);
    }

    // 6. A fully Turkish sentence -> no palochka or homoglyph findings (the scope rule's own test).
    [TestMethod]
    public void Test6_FullyTurkishSentence_ProducesNoAdygheSpecificFindings()
    {
        var result = TextAnalyzer.Analyze("Bugün güzel bir gün, ılık ve sakin.", TestProfiles.Ady);
        Assert.AreEqual(0, result.Findings.Count(f => f.Code is FindingCodes.PalochkaLookAlike or FindingCodes.PalochkaCase
            or FindingCodes.NonAlphabet or FindingCodes.Confusable));
    }

    // 9. "* madde bir\n* madde iki" -> two sentences, not one.
    [TestMethod]
    public void Test9_BulletedLines_SplitIntoTwoSentences()
    {
        var result = TextAnalyzer.Analyze("* madde bir\n* madde iki", TestProfiles.Ady);
        Assert.AreEqual(2, result.Sentences.Count);
        Assert.AreEqual("madde bir", result.Sentences[0].Text);
        Assert.AreEqual("madde iki", result.Sentences[1].Text);
    }

    // 10. "«Непэ" -> "Непэ"; "Anthropic-м" stays one token.
    [TestMethod]
    public void Test10_LeadingGuillemetTrimmed_AndHyphenatedWordStaysWhole()
    {
        var result = TextAnalyzer.Analyze("«Непэ Anthropic-м", TestProfiles.Ady);
        var tokens = result.Sentences[0].Tokens;
        Assert.AreEqual("Непэ", tokens[0].Text);
        Assert.AreEqual("Anthropic-м", tokens[1].Text);
    }

    // 11. Ordering test: repair must run before tokenisation, or a "|" standing in for a palochka
    // inflates the word count. Compared here against a naive word-boundary split on whitespace and
    // "|" together (the kind of split a tokeniser that ran before repair, and treated "|" as a
    // divider the way the project docs describe -- "| mistaken for punctuation" -- would produce).
    [TestMethod]
    public void Test11_RepairBeforeTokenize_AvoidsWordCountInflation()
    {
        const string raw = "къ|уэ";

        var naiveWordCountWithoutRepair = raw.Split([' ', '|'], StringSplitOptions.RemoveEmptyEntries).Length;

        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);

        Assert.AreEqual(2, naiveWordCountWithoutRepair, "sanity check on the naive baseline");
        Assert.AreEqual(1, result.Metrics.WordCount, "repair-before-tokenise must keep this as one word");
        Assert.IsTrue(naiveWordCountWithoutRepair > result.Metrics.WordCount);
        Assert.AreEqual("къӏуэ", result.Sentences[0].Tokens[0].Text);
    }
}
