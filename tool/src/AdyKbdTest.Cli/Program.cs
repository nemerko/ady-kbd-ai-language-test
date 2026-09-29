using AdyKbdTest.Cli;
using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;

var Usage = $$"""
    {{AppInfo.DisplayName}} ({{AppInfo.Version}}) -- CLI command: akt

    Usage:
      akt analyze  --raw <dir> --test <n> [--profile ady] [--out <dir=data/analysis>] [--profiles <dir=profiles>]
      akt workbook --raw <dir> --test <n> [--profile ady] [--out <dir-or-file=data/workbooks>] [--profiles <dir=profiles>]
      akt report   --workbook <file.xlsx> --analysis <dir> [--out <dir=data/analysis>]
      akt verify   --raw <dir>
      akt version

    "akt workbook" never silently overwrites a marking workbook that already has evaluator marks
    in it; it writes a numbered alternate file instead. There is no override for this.

    Exit codes: 0 success, 1 user error, 2 unexpected error.
    """;

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

var command = args[0];

try
{
    var options = CommandLineOptions.Parse(args, 1);
    return command switch
    {
        "analyze" => Commands.Analyze(options),
        "workbook" => Commands.Workbook(options),
        "report" => Commands.Report(options),
        "verify" => Commands.Verify(options),
        "version" or "-v" or "--version" => PrintVersion(),
        "-h" or "--help" or "help" => PrintUsage(),
        _ => UnknownCommand(command),
    };
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage);
    return 1;
}
catch (AlphabetValidationException ex)
{
    Console.Error.WriteLine($"Alphabet source validation failed: {ex.Message}");
    return 1;
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 2;
}

int PrintUsage()
{
    Console.WriteLine(Usage);
    return 0;
}

int PrintVersion()
{
    Console.WriteLine($"{AppInfo.DisplayName} {AppInfo.Version}");
    return 0;
}

int UnknownCommand(string name)
{
    Console.Error.WriteLine($"Unknown command \"{name}\".");
    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage);
    return 1;
}
