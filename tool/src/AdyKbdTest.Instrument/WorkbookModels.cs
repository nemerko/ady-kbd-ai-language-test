using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Instrument;

/// <summary>Input to <see cref="WorkbookBuilder"/>: one test's worth of resolved result files, each with its seven analysed responses.</summary>
public sealed record WorkbookInput(int Test, IReadOnlyList<ResolvedResultFile> ResolvedFiles, IReadOnlyDictionary<ResponseId, AnalyzedResponse> Analyzed);

public enum WorkbookWriteOutcome
{
    /// <summary>No file existed at the target path; it was created.</summary>
    Created,
    /// <summary>A file existed but carried no evaluator marks; it was overwritten.</summary>
    OverwrittenEmpty,
    /// <summary>A file existed and carried evaluator marks; it was left untouched and a numbered alternate was written instead.</summary>
    KeptExistingWroteNumbered,
}

/// <summary>What <see cref="WorkbookBuilder.BuildWithOverwriteProtection"/> actually did.</summary>
public sealed record WorkbookWriteResult(string WrittenPath, WorkbookWriteOutcome Outcome, string? PreservedExistingPath);
