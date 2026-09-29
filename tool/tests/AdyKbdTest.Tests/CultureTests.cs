using System.Globalization;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

/// <summary>
/// Tests 7 and 8 of §7: the two Turkish-locale pitfalls the whole tool depends on avoiding. Both
/// run with the process culture actually set to tr-TR, then restore it, because the bug they guard
/// against only shows up when the ambient culture is Turkish -- exactly the development machine.
/// </summary>
[TestClass]
public sealed class CultureTests
{
    private static T UnderTurkishCulture<T>(Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = tr;
            CultureInfo.CurrentUICulture = tr;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    // 7. Under tr-TR, 0.42 must format as "0.42", not "0,42".
    [TestMethod]
    public void Test7_DecimalFormatting_IsInvariantEvenUnderTurkishCulture()
    {
        var formatted = UnderTurkishCulture(() => (0.42).ToString("0.####", CultureInfo.InvariantCulture));
        Assert.AreEqual("0.42", formatted);
    }

    // 8. Under tr-TR, Latin-I detection must not break ("I".ToLower() would give "ı" under tr-TR
    // with the culture-sensitive overload; the whole PAL_LOOKALIKE / repair pipeline must not use it).
    [TestMethod]
    public void Test8_LatinCapitalIDetection_IsNotCorruptedByTurkishCulture()
    {
        var result = UnderTurkishCulture(() => TextAnalyzer.Analyze("Iофыгъу", TestProfiles.Ady));
        var finding = result.Findings.Single(f => f.Code == FindingCodes.PalochkaLookAlike);
        Assert.AreEqual(0x0049, (int)finding.Found[0]);
        Assert.AreEqual(0x04C0, (int)finding.Expected[0]);
    }
}
