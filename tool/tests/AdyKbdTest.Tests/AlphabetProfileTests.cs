using AdyKbdTest.Instrument;

namespace AdyKbdTest.Tests;

/// <summary>Tests 12-15 of §7, on the vendored alphabet sources themselves.</summary>
[TestClass]
public sealed class AlphabetProfileTests
{
    // 12. ady main has 66 entries, kbd main has 59; both have 3 auxiliary entries and 34 characters.
    [TestMethod]
    public void Test12_LetterCounts_MatchThePublishedSource()
    {
        var ady = ProfileLoader.Load(TestPaths.ProfilesDir, "ady");
        var kbd = ProfileLoader.Load(TestPaths.ProfilesDir, "kbd");

        Assert.AreEqual(66, ady.Alphabet.Main.Count);
        Assert.AreEqual(59, kbd.Alphabet.Main.Count);
        Assert.AreEqual(3, ady.Alphabet.Auxiliary.Count);
        Assert.AreEqual(3, kbd.Alphabet.Auxiliary.Count);
        Assert.AreEqual(34, ady.Alphabet.Characters.Count);
        Assert.AreEqual(34, kbd.Alphabet.Characters.Count);
    }

    // 13. characters.lower must equal what is derivable from the single-character entries of "main".
    // AlphabetProfile.Parse already enforces this at load time (throws on mismatch); loading without
    // throwing, plus this direct re-derivation, is the test.
    [TestMethod]
    public void Test13_CharactersLower_MatchesSingleCharacterMainEntries()
    {
        var profile = ProfileLoader.Load(TestPaths.ProfilesDir, "ady");
        var derived = string.Concat(profile.Alphabet.Main
            .Where(l => l.Type == "single")
            .OrderBy(l => l.AlphabetIndex)
            .Select(l => l.CyrlLower));

        Assert.AreEqual(derived, profile.Alphabet.Characters.Lower);
    }

    // 14. "кӏ" entry: cyrl_title and cyrl_upper must be different strings, neither derived from the other.
    [TestMethod]
    public void Test14_MultiCharacterLetter_TitleAndUpperAreDistinctStrings()
    {
        var profile = ProfileLoader.Load(TestPaths.ProfilesDir, "ady");
        var kI = profile.Alphabet.Main.Single(l => l.CyrlLower == "кӏ");

        Assert.AreEqual("Кӏ", kI.CyrlTitle);
        Assert.AreEqual("КӀ", kI.CyrlUpper);
        Assert.AreNotEqual(kI.CyrlTitle, kI.CyrlUpper);
    }

    // 15. The vendored alphabet files' SHA-256 must match the value recorded in the tool profile.
    // Parse() enforces this and throws AlphabetValidationException on mismatch; a successful load is the test.
    [TestMethod]
    public void Test15_VendoredAlphabetFiles_MatchRecordedSha256()
    {
        _ = ProfileLoader.Load(TestPaths.ProfilesDir, "ady");
        _ = ProfileLoader.Load(TestPaths.ProfilesDir, "kbd");
        // No AlphabetValidationException means both SHA-256 checks passed.
    }
}
