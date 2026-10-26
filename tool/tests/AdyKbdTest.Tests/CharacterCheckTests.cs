using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>
/// Character-level checks (NON_ALPHABET, CONFUSABLE, NORMALIZATION, INVISIBLE, PAL_CASE) and the
/// Levenshtein similarity utility.
///
/// Test strings that hinge on one exact code point are built from \u escapes rather than typed
/// glyphs, because several of these characters have visually identical look-alikes (that is the
/// whole subject of the tests) and a typo in the source file would silently test the wrong thing.
/// </summary>
[TestClass]
public sealed class CharacterCheckTests
{
    [TestMethod]
    public void Confusable_LatinLetterInsideCyrillicWord_IsFlagged()
    {
        // "программа" (programme) with a Latin "a" (U+0061) substituted for Cyrillic "а" (U+0430).
        var raw = "прогр" + "a" + "мма";
        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);
        var finding = result.Findings.Single(f => f.Code == FindingCodes.Confusable);
        Assert.AreEqual("a", finding.Found);
        Assert.AreEqual("а", finding.Expected);
    }

    [TestMethod]
    public void NonAlphabet_CyrillicLetterOutsideTheAlphabet_IsFlagged()
    {
        // CYRILLIC SMALL LETTER DZE (U+0455) is not in the Adyghe alphabet's 34-character inventory.
        var raw = "неп" + "ѕ" + "э";
        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);
        var finding = result.Findings.Single(f => f.Code == FindingCodes.NonAlphabet);
        Assert.AreEqual("ѕ", finding.Found);
    }

    [TestMethod]
    public void Normalization_DetachedYo_IsFlaggedAndNormalized()
    {
        // "ё" written as CYRILLIC е (U+0435) + COMBINING DIAERESIS (U+0308) instead of precomposed.
        var raw = "ё";
        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);
        var finding = result.Findings.Single(f => f.Code == FindingCodes.Normalization);
        Assert.AreEqual("ё", finding.Expected); // ё
        Assert.IsFalse(result.WasNfc);
    }

    [TestMethod]
    public void Invisible_ZeroWidthJoiner_IsRemovedAndFlagged()
    {
        var raw = "не" + "‍" + "пэ"; // ZWJ (Cf category) inside a word
        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);
        Assert.IsTrue(result.Findings.Any(f => f.Code == FindingCodes.Invisible));
        Assert.IsFalse(result.RepairedText.Contains('‍'));
    }

    [TestMethod]
    public void PalochkaCase_UpperMidWord_IsFlaggedLowWeight()
    {
        // к + palochka UPPER (U+04C0, mid-word -- wrong) + уэ.
        var raw = "к" + "Ӏ" + "уэ";
        var result = TextAnalyzer.Analyze(raw, TestProfiles.Ady);
        var finding = result.Findings.Single(f => f.Code == FindingCodes.PalochkaCase);
        Assert.AreEqual(FindingSeverity.Low, finding.Severity);
    }

    [TestMethod]
    public void Levenshtein_IdenticalStrings_SimilarityIsOne()
    {
        Assert.AreEqual(1.0, LevenshteinSimilarity.NormalizedSimilarity("abc", "abc"));
    }

    [TestMethod]
    public void Levenshtein_CompletelyDifferentStrings_SimilarityIsZero()
    {
        Assert.AreEqual(0.0, LevenshteinSimilarity.NormalizedSimilarity("abc", "xyz"));
    }

    [TestMethod]
    public void Levenshtein_OneDeletedCharacter_GivesExpectedDistance()
    {
        Assert.AreEqual(1, LevenshteinSimilarity.Distance("abcd", "abc"));
    }
}
