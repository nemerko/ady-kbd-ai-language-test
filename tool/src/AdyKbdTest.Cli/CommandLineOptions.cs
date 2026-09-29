namespace AdyKbdTest.Cli;

/// <summary>Minimal hand-written "--key value" parser. No command-line library (§2, deliberately).</summary>
internal sealed class CommandLineOptions
{
    private readonly Dictionary<string, string> _values = [];

    private CommandLineOptions() { }

    public static CommandLineOptions Parse(string[] args, int startIndex)
    {
        var options = new CommandLineOptions();
        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument \"{arg}\" (expected \"--option value\").");
            }
            var key = arg[2..];
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Option \"--{key}\" is missing its value.");
            }
            options._values[key] = args[++i];
        }
        return options;
    }

    public string Require(string key) =>
        _values.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing required option \"--{key}\".");

    public string GetOrDefault(string key, string defaultValue) =>
        _values.GetValueOrDefault(key, defaultValue);

    public int RequireInt(string key)
    {
        var raw = Require(key);
        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"Option \"--{key}\" must be an integer, got \"{raw}\".");
    }
}
