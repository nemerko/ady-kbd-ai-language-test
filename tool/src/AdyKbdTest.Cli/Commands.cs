using AdyKbdTest.Instrument;

namespace AdyKbdTest.Cli;

internal static class Commands
{
    public static int Analyze(CommandLineOptions options)
    {
        var raw = options.Require("raw");
        var test = options.RequireInt("test");
        var language = options.GetOrDefault("profile", "ady");
        var profilesDir = options.GetOrDefault("profiles", "profiles");
        var outDir = options.GetOrDefault("out", "data/analysis");

        var (_, resolvedFiles, analyzed) = AnalysisRunner.Run(profilesDir, language, raw, test);

        var records = analyzed
            .Select(kv => AnalysisRecord.From(kv.Key, kv.Value))
            .OrderBy(r => r.Tool).ThenBy(r => r.Question)
            .ToList();

        Directory.CreateDirectory(outDir);
        var jsonPath = Path.Combine(outDir, $"test{test}-analysis.json");
        var tsvPath = Path.Combine(outDir, $"test{test}-analysis.tsv");
        AnalysisExport.WriteJson(jsonPath, records);
        AnalysisExport.WriteTsv(tsvPath, records);

        Console.WriteLine($"Analyzed {resolvedFiles.Count} tool(s) x 7 questions = {records.Count} responses for test {test}.");
        Console.WriteLine($"Wrote {jsonPath}");
        Console.WriteLine($"Wrote {tsvPath}");

        foreach (var resolved in resolvedFiles)
        {
            if (resolved.AllAttempts.Count > 1)
            {
                Console.WriteLine($"  tool {resolved.Selected.Tool}: {resolved.AllAttempts.Count} attempt(s) found, using attempt {resolved.Selected.Attempt} ({resolved.Selected.SourceFileName}).");
            }
            if (!resolved.Selected.AllAnswersNonEmpty)
            {
                Console.WriteLine($"  WARNING: tool {resolved.Selected.Tool}: no attempt has all seven answers non-empty; using the best available ({resolved.Selected.SourceFileName}).");
            }
        }

        return 0;
    }

    public static int Workbook(CommandLineOptions options)
    {
        var raw = options.Require("raw");
        var test = options.RequireInt("test");
        var language = options.GetOrDefault("profile", "ady");
        var profilesDir = options.GetOrDefault("profiles", "profiles");
        var outArg = options.GetOrDefault("out", "data/workbooks");

        var outputPath = outArg.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? outArg
            : Path.Combine(outArg, $"test{test}-marking.xlsx");

        var (_, resolvedFiles, analyzed) = AnalysisRunner.Run(profilesDir, language, raw, test);

        var result = WorkbookBuilder.BuildWithOverwriteProtection(outputPath, new WorkbookInput(test, resolvedFiles, analyzed));

        switch (result.Outcome)
        {
            case WorkbookWriteOutcome.Created:
                Console.WriteLine($"Wrote {result.WrittenPath} ({resolvedFiles.Count} tool(s) x 7 questions).");
                break;
            case WorkbookWriteOutcome.OverwrittenEmpty:
                Console.WriteLine($"Overwrote {result.WrittenPath}: the existing file carried no evaluator marks.");
                break;
            case WorkbookWriteOutcome.KeptExistingWroteNumbered:
                Console.WriteLine($"NOT overwritten: {result.PreservedExistingPath} already carries evaluator marks.");
                Console.WriteLine($"Wrote the new workbook to {result.WrittenPath} instead.");
                break;
        }

        return 0;
    }

    public static int Report(CommandLineOptions options)
    {
        var workbook = options.Require("workbook");
        var analysisDir = options.Require("analysis");
        var outDir = options.GetOrDefault("out", "data/analysis");

        ReportBuilder.Build(workbook, analysisDir, outDir);

        Console.WriteLine($"Wrote report tables to {outDir}");
        return 0;
    }

    public static int Verify(CommandLineOptions options)
    {
        var raw = options.Require("raw");
        if (!Directory.Exists(raw))
        {
            Console.Error.WriteLine($"Directory not found: {raw}");
            return 1;
        }

        var files = Directory.GetFiles(raw, "Test*-AI-Tool*-Result*.txt").OrderBy(f => f, StringComparer.Ordinal).ToList();
        Console.WriteLine($"Found {files.Count} result file(s) in {raw}.");

        var hadProblems = false;
        var parsed = new List<RawResultFile>();

        foreach (var file in files)
        {
            try
            {
                var result = ResultFileParser.Parse(file);
                parsed.Add(result);
                if (!result.AllAnswersNonEmpty)
                {
                    Console.WriteLine($"  INCOMPLETE: {Path.GetFileName(file)} does not have all seven answers non-empty.");
                }
            }
            catch (ResultFileFormatException ex)
            {
                hadProblems = true;
                Console.WriteLine($"  FORMAT ERROR: {ex.Message}");
            }
        }

        foreach (var testGroup in parsed.GroupBy(f => f.Test).OrderBy(g => g.Key))
        {
            foreach (var toolGroup in testGroup.GroupBy(f => f.Tool).OrderBy(g => g.Key))
            {
                var attempts = toolGroup.OrderBy(f => f.Attempt).ToList();
                if (attempts.Count > 1)
                {
                    var winner = attempts.Where(f => f.AllAnswersNonEmpty).OrderByDescending(f => f.Attempt).FirstOrDefault() ?? attempts.OrderByDescending(f => f.Attempt).First();
                    Console.WriteLine($"  Test {testGroup.Key}, tool {toolGroup.Key}: {attempts.Count} attempts -> using {winner.SourceFileName}.");
                }
                if (attempts.All(f => !f.AllAnswersNonEmpty))
                {
                    Console.WriteLine($"  WARNING: Test {testGroup.Key}, tool {toolGroup.Key}: no attempt has all seven answers non-empty.");
                }
            }
        }

        Console.WriteLine(hadProblems ? "Verification found format errors." : "Verification found no format errors.");
        return hadProblems ? 1 : 0;
    }
}
