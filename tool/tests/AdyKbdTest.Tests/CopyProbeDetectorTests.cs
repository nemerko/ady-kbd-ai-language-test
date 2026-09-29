using AdyKbdTest.Instrument;

namespace AdyKbdTest.Tests;

/// <summary>
/// Test strings are built from the same short, independently-verified fragments (see the
/// PowerShell code-point dump in the session that fixed a schwa/e mix-up in the detector's own
/// constant) rather than typed sentences, since a typo in either would silently make the wrong
/// assertion true.
/// </summary>
[TestClass]
public sealed class CopyProbeDetectorTests
{
    // CYRILLIC: н(043D) ы(044B) с(0441) а(0430) щ(0449) -- "нысащ", the wedding root.
    private const string OriginalRoot = "нысащ";

    // CYRILLIC: х(0445) ъ(044A) у(0443) г(0433) ъ(044A) э(044D) space м(043C) а(0430) ф(0444) э(044D)
    // -- "хъугъэ мафэ", the birthday phrase.
    private const string ChangedRoot = "хъугъэ мафэ";

    [TestMethod]
    public void OriginalRootOnly_ReturnsOzgunGeriVerdi()
    {
        Assert.AreEqual(CopyProbeResult.OZGUN_GERI_VERDI, CopyProbeDetector.Detect($"xxx {OriginalRoot}э yyy"));
    }

    [TestMethod]
    public void ChangedRootOnly_ReturnsDegisiklikYansitti()
    {
        Assert.AreEqual(CopyProbeResult.DEGISIKLIGI_YANSITTI, CopyProbeDetector.Detect($"xxx {ChangedRoot}м yyy"));
    }

    [TestMethod]
    public void NeitherRootPresent_ReturnsBelirsiz()
    {
        Assert.AreEqual(CopyProbeResult.BELIRSIZ, CopyProbeDetector.Detect("xxx yyy zzz"));
    }

    [TestMethod]
    public void BothRootsPresent_ReturnsBelirsiz()
    {
        Assert.AreEqual(CopyProbeResult.BELIRSIZ, CopyProbeDetector.Detect($"{OriginalRoot}э ... {ChangedRoot}м"));
    }

    // Real data: Test2-AI-Tool4's S5 answer uses "икъыщыхъугъэ мафэм" -- the changed (birthday) wording.
    [TestMethod]
    public void RealData_Test2Tool4_ReflectsTheChange()
    {
        var path = Path.Combine(TestPaths.RawRound1Dir, "Test2-AI-Tool4-Result.txt");
        var file = ResultFileParser.Parse(path);
        var s5 = file.Questions.Single(q => q.Number == 5);

        var result = CopyProbeDetector.Detect(s5.AnswerText);
        Assert.AreEqual(CopyProbeResult.DEGISIKLIGI_YANSITTI, result);
    }

    // Real data: Test1-AI-Tool2 (Claude)'s S5 answer keeps the original wedding wording.
    [TestMethod]
    public void RealData_Test1Tool2_ReturnsOriginal()
    {
        var path = Path.Combine(TestPaths.RawRound1Dir, "Test1-AI-Tool2-Result.txt");
        var file = ResultFileParser.Parse(path);
        var s5 = file.Questions.Single(q => q.Number == 5);

        var result = CopyProbeDetector.Detect(s5.AnswerText);
        Assert.AreEqual(CopyProbeResult.OZGUN_GERI_VERDI, result);
    }
}
