using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Instrument;

public sealed record SentenceMarking
{
    public required int? SentenceNo { get; init; }
    public required string? SentenceText { get; init; }
    public required int? WordCount { get; init; }
    public required SentenceVerdict? Verdict { get; init; }
    public required bool VerdictBlank { get; init; }
    public required int? WrongForm { get; init; }
    public required int? WrongWord { get; init; }
    public required int? Invented { get; init; }
    public required int? CodeMixing { get; init; }
    public required int? TermChoice { get; init; }
    public required int? KbdCount { get; init; }
    public required int? RusCount { get; init; }
    public required int? TurCount { get; init; }
    public required int? EngCount { get; init; }
    public required int? OtherCount { get; init; }
    public required string? Note { get; init; }

    /// <summary>
    /// Revision 6 §10.3: paste bypasses Excel's own data validation, so a numeric-entry cell can end
    /// up holding text that will never parse -- which <see cref="WorkbookXmlHelpers"/>'s int parsing
    /// otherwise folds into the same "null" a blank cell produces, silently. The column letters of
    /// every such cell on this sentence are collected here so <c>ReportBuilder</c> can report
    /// <c>NON_NUMERIC</c> instead of treating the cell as merely unmarked.
    /// </summary>
    public required IReadOnlyList<string> NonNumericColumns { get; init; }

    /// <summary>
    /// Correct words = word count - (FRM+CHO+INV+MIX). Excludes term choice (M) deliberately: TRM
    /// is a correctly formed word, not an error. Computed here rather than trusted from the sheet's
    /// own formula cell, because a formula cell's cached value can be stale (Excel has not
    /// recalculated) or, in a freshly generated and never-opened workbook, is just the "0"
    /// placeholder <see cref="WorkbookBuilder"/> writes as every formula cell's cached value.
    /// </summary>
    public int CorrectWords => (WordCount ?? 0) - ((WrongForm ?? 0) + (WrongWord ?? 0) + (Invented ?? 0) + (CodeMixing ?? 0));

    /// <summary>Sum of the four listed contact languages plus "other" -- computed, not read from the sheet's formula cell, for the same reason as <see cref="CorrectWords"/>.</summary>
    public int TotalContactLanguageWords => (KbdCount ?? 0) + (RusCount ?? 0) + (TurCount ?? 0) + (EngCount ?? 0) + (OtherCount ?? 0);

    /// <summary>The four listed contact languages with a positive count in this sentence. Does not include "other" (§4 item 1's four are individually named; "other" is a total, not a language).</summary>
    public IReadOnlyList<ContactLanguage> ContactLanguagesPresent =>
        [
            .. (KbdCount ?? 0) > 0 ? [ContactLanguage.KBD] : Array.Empty<ContactLanguage>(),
            .. (RusCount ?? 0) > 0 ? [ContactLanguage.RUS] : Array.Empty<ContactLanguage>(),
            .. (TurCount ?? 0) > 0 ? [ContactLanguage.TUR] : Array.Empty<ContactLanguage>(),
            .. (EngCount ?? 0) > 0 ? [ContactLanguage.ENG] : Array.Empty<ContactLanguage>(),
        ];
}

public sealed record ResponseMarking
{
    public required ResponseId Id { get; init; }
    public required LanguageComplianceWord? LanguageCompliance { get; init; }
    public required SemanticAdequacyWord? Semantic { get; init; }
    public required MarkedWord? Marked { get; init; }
    public required string? Note { get; init; }
    /// <summary>Only meaningful on question 7's rows (§5 item 3); null everywhere else, including when question 7 simply has not been marked yet.</summary>
    public required Question7Verdict? Question7Verdict { get; init; }
    public required IReadOnlyList<SentenceMarking> Sentences { get; init; }
}

/// <summary>
/// Reads a filled-in marking workbook back: the Responses sheet (one row per response) joined,
/// by key, with the Sentences sheet (one row per sentence, self-contained -- every row carries its
/// own Key/Question/Tool, so unlike the single-sheet design this replaces, no "which block is this
/// row part of" inference is needed). Handles both inline strings (as <see cref="WorkbookBuilder"/>
/// writes them) and shared strings (as Excel commonly rewrites them to on save).
///
/// Column letters (revision 4 §1/§3): Sentences H-K error counts, L correct words (formula, not
/// read -- see <see cref="SentenceMarking.CorrectWords"/>), M term choice, N-Q the four listed
/// contact languages, R other languages (a single count), S total (formula, not read), T note.
/// Responses I-K unchanged, L question 7 verdict, M note.
/// </summary>
public static class WorkbookReader
{
    public static IReadOnlyList<ResponseMarking> Read(string path)
    {
        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart!;
        var sstPart = workbookPart.SharedStringTablePart;

        string? Text(Row row, string column) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, column), sstPart);

        var responseRows = new Dictionary<ResponseId, (LanguageComplianceWord? Lang, SemanticAdequacyWord? Sem, MarkedWord? Marked, Question7Verdict? Q7, string? Note)>();
        foreach (var row in WorkbookXmlHelpers.ReadRows(workbookPart, WorkbookBuilder.ResponsesSheetName))
        {
            if (row.RowIndex?.Value is null or 1) continue;
            var keyText = Text(row, "A");
            if (string.IsNullOrWhiteSpace(keyText) || !ResponseId.TryParse(keyText.Trim(), out var id)) continue;

            responseRows[id] = (
                CodingSchemeText.ParseDropdownValue(CodingSchemeText.LanguageCompliance, Text(row, "I")),
                CodingSchemeText.ParseDropdownValue(CodingSchemeText.SemanticAdequacy, Text(row, "J")),
                CodingSchemeText.ParseDropdownValue(CodingSchemeText.Marked, Text(row, "K")),
                id.Question == 7 ? CodingSchemeText.ParseDropdownValue(CodingSchemeText.Question7Verdicts, Text(row, "L")) : null,
                Text(row, "M"));
        }

        var sentencesByResponse = new Dictionary<ResponseId, List<SentenceMarking>>();
        foreach (var row in WorkbookXmlHelpers.ReadRows(workbookPart, WorkbookBuilder.SentencesSheetName))
        {
            if (row.RowIndex?.Value is null or 1) continue;
            var keyText = Text(row, "A");
            if (string.IsNullOrWhiteSpace(keyText) || !ResponseId.TryParse(keyText.Trim(), out var id)) continue;

            var verdictText = Text(row, "G");
            var verdict = CodingSchemeText.ParseDropdownValue(CodingSchemeText.SentenceVerdicts, verdictText);

            // Revision 6 §10.3: track which numeric-entry columns hold non-blank text that failed to
            // parse, so a pasted-in "n/a" is reported as NON_NUMERIC rather than silently read as blank.
            var nonNumericColumns = new List<string>();
            int? ParseCount(string column)
            {
                var (value, nonNumeric) = ParseIntOrNull(Text(row, column));
                if (nonNumeric) nonNumericColumns.Add(column);
                return value;
            }

            var marking = new SentenceMarking
            {
                SentenceNo = ParseIntOrNull(Text(row, "D")).Value,
                SentenceText = Text(row, "E"),
                WordCount = ParseIntOrNull(Text(row, "F")).Value,
                Verdict = verdict,
                VerdictBlank = verdict is null,
                WrongForm = ParseCount("H"),
                WrongWord = ParseCount("I"),
                Invented = ParseCount("J"),
                CodeMixing = ParseCount("K"),
                TermChoice = ParseCount("M"),
                KbdCount = ParseCount("N"),
                RusCount = ParseCount("O"),
                TurCount = ParseCount("P"),
                EngCount = ParseCount("Q"),
                OtherCount = ParseCount("R"),
                Note = Text(row, "T"),
                NonNumericColumns = nonNumericColumns,
            };

            if (!sentencesByResponse.TryGetValue(id, out var list)) sentencesByResponse[id] = list = [];
            list.Add(marking);
        }

        var allKeys = responseRows.Keys.Union(sentencesByResponse.Keys);
        var result = new List<ResponseMarking>();
        foreach (var id in allKeys)
        {
            responseRows.TryGetValue(id, out var response);
            sentencesByResponse.TryGetValue(id, out var sentences);
            result.Add(new ResponseMarking
            {
                Id = id,
                LanguageCompliance = response.Lang,
                Semantic = response.Sem,
                Marked = response.Marked,
                Question7Verdict = response.Q7,
                Note = response.Note,
                Sentences = sentences ?? [],
            });
        }
        return result;
    }

    /// <summary>
    /// Blank -&gt; (null, false). A number -&gt; (n, false). Non-blank text that is not a number
    /// (only reachable by paste, since Excel's own validation blocks typing it -- revision 6 §10.3)
    /// -&gt; (null, true), so callers can tell "not entered" apart from "entered garbage".
    /// </summary>
    private static (int? Value, bool NonNumeric) ParseIntOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, false);
        return int.TryParse(value.Trim(), out var n) ? (n, false) : (null, true);
    }
}
