namespace AdyKbdTest.Instrument;

/// <summary>One of the seven Q/A blocks in a result file.</summary>
public sealed record RawQuestion
{
    public required int Number { get; init; }

    /// <summary>The full question block as it stands in the file (may include a pasted source paragraph and its control translation), trimmed.</summary>
    public required string QuestionText { get; init; }

    /// <summary>The Turkish translation given in parentheses at the end of the question's first line, if any. This is the recorder's reference translation; it was never sent to the tool.</summary>
    public string? TurkishTranslation { get; init; }

    /// <summary>The answer body, trimmed. An empty string is a valid, recorded state (the session may have been interrupted).</summary>
    public required string AnswerText { get; init; }

    /// <summary>Text that shared the "CEVAP S{n}:" line itself -- a recorder's note (e.g. a timestamp or an error message), not part of the answer.</summary>
    public string? RecorderNote { get; init; }
}

/// <summary>One parsed "Test{n}-AI-Tool{m}-Result[k].txt" file.</summary>
public sealed record RawResultFile
{
    public required int Test { get; init; }
    public required int Tool { get; init; }

    /// <summary>1 for "...Result.txt", 2 for "...Result2.txt", and so on. Only the highest-numbered attempt whose seven answers are all non-empty is used for measurement (§5.1).</summary>
    public required int Attempt { get; init; }

    public required string SourceFileName { get; init; }
    public required string SourcePath { get; init; }
    public required IReadOnlyDictionary<string, string> HeaderFields { get; init; }
    public required IReadOnlyList<RawQuestion> Questions { get; init; }

    public bool AllAnswersNonEmpty => Questions.Count == 7 && Questions.All(q => !string.IsNullOrWhiteSpace(q.AnswerText));
}
