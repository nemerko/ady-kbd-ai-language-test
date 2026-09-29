using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Tests;

internal static class TestProfiles
{
    private static LanguageProfile? _ady;
    public static LanguageProfile Ady => _ady ??= ProfileLoader.Load(TestPaths.ProfilesDir, "ady");
}
