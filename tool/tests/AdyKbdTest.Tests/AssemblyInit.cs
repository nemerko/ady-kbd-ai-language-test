using System.Globalization;

namespace AdyKbdTest.Tests;

/// <summary>
/// The whole test run happens under tr-TR, not just the two dedicated culture tests (§7's own
/// requirement: "testler, süreç kültürü tr-TR'ye ayarlanmışken de geçmeli"). This is the strongest
/// version of that check -- every test in the assembly, not only the ones written to think about it.
/// </summary>
[TestClass]
public static class AssemblyInit
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = turkish;
        CultureInfo.DefaultThreadCurrentUICulture = turkish;
        CultureInfo.CurrentCulture = turkish;
        CultureInfo.CurrentUICulture = turkish;
    }
}
