namespace AdyKbdTest.Instrument;

/// <summary>
/// The tool's human-facing display name and version (revision 1, §0.1). Used everywhere a person
/// reads it: the CLI's help/version banner, generated report titles, and the
/// marking workbook's Info-sheet credit line. Code identifiers (the .slnx name, the AdyKbdTest.*
/// namespaces, the "akt" command) are unaffected -- short names were kept there deliberately
/// because C# identifiers cannot contain a hyphen and because renaming them would touch every file.
/// </summary>
public static class AppInfo
{
    public const string DisplayName = "ady-kbd AI Language Testing Tool";
    public const string Version = "1.0.0";
}
