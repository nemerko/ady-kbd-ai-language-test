using System.Globalization;
using System.Text;
using System.Text.Json;
using AdyKbdTest.TextAnalysis;

namespace AdyKbdTest.Instrument;

public sealed record IntegrityWarning(string Kind, string Detail);

/// <summary>
/// Combines a filled-in marking workbook (Responses + Sentences sheets) with the character-level
/// analysis already written to data/analysis and produces the report tables: tool x category
/// (summed, not counted, since errors are per-sentence counts under revision 1), sentence-verdict
/// distribution, language-compliance and semantic-adequacy distribution, the mechanical copying
/// probe (read from the analysis output, not the workbook -- revision 1 removed its workbook
/// column), cross-test stability, a character-layer summary, and a separate, un-hidden list of
/// data-integrity warnings.
/// </summary>
public static class ReportBuilder
{
    public static void Build(string workbookPath, string analysisDir, string outDir)
    {
        Directory.CreateDirectory(outDir);

        var markings = WorkbookReader.Read(workbookPath);
        if (markings.Count == 0)
        {
            throw new InvalidOperationException($"No responses could be read from {workbookPath}.");
        }

        var test = markings[0].Id.Test;
        var allRecords = LoadAllAnalysisJson(analysisDir);
        var recordsById = allRecords.ToDictionary(r => r.Id, r => r);

        var warnings = CollectIntegrityWarnings(markings, recordsById);
        WriteIntegrityWarnings(Path.Combine(outDir, $"test{test}-integrity-warnings.tsv"), warnings);

        WriteToolCategoryTable(Path.Combine(outDir, $"test{test}-tool-x-category.tsv"), markings, recordsById, test);
        WriteContactLanguagesTable(Path.Combine(outDir, $"test{test}-contact-languages.tsv"), markings);
        WriteToolQuestionTable(Path.Combine(outDir, $"test{test}-tool-x-question.tsv"), markings, recordsById);
        WriteExcludedResponsesTable(Path.Combine(outDir, $"test{test}-excluded-responses.tsv"), markings, recordsById);
        WriteSentenceVerdictDistribution(Path.Combine(outDir, $"test{test}-sentence-verdicts.tsv"), markings);
        WriteLanguageComplianceDistribution(Path.Combine(outDir, $"test{test}-language-compliance.tsv"), markings);
        WriteCopyProbeResult(Path.Combine(outDir, $"test{test}-copy-probe.tsv"), recordsById, test);
        WriteQuestion7VerdictResult(Path.Combine(outDir, $"test{test}-q7-verdict.tsv"), markings);
        WriteCharacterLayerSummary(Path.Combine(outDir, $"test{test}-character-summary.tsv"), recordsById, test);
        WriteStability(Path.Combine(outDir, "stability.tsv"), allRecords);

        WriteHumanSummary(Path.Combine(outDir, $"test{test}-report-summary.txt"), test, markings, recordsById, warnings);
    }

    /// <summary>
    /// Revision 13 §2: a response with no evaluable content -- not in the target language AND not
    /// addressing the question -- is outside the measurement. Its sentences carry no labels (the
    /// marking instructions tell evaluators not to label them, revision 6 §10.2), so counting its
    /// words in a denominator would credit the tool for text that was never evaluated. Both
    /// conditions are required: UYUM_YOK alone means different things in different questions
    /// (Adyghe for Q1-Q3 and Q5-Q7, Turkish for Q4, since that is the target language there), while
    /// ALAKASIZ is question-independent. This is the single source of the exclusion rule --
    /// <see cref="WriteToolCategoryTable"/>, <see cref="WriteToolQuestionTable"/>,
    /// <see cref="WriteExcludedResponsesTable"/> and the <c>BLANK_SENTENCE_VERDICT</c> suppression
    /// in <see cref="CollectIntegrityWarnings"/> all call this one predicate rather than each
    /// re-deriving the condition, which is what let them silently disagree before this revision.
    /// </summary>
    private static bool HasNoEvaluableContent(ResponseMarking m) =>
        m.LanguageCompliance == LanguageComplianceWord.UyumYok
        && m.Semantic == SemanticAdequacyWord.Alakasiz;

    private static List<AnalysisRecord> LoadAllAnalysisJson(string analysisDir)
    {
        var result = new List<AnalysisRecord>();
        if (!Directory.Exists(analysisDir)) return result;
        foreach (var file in Directory.GetFiles(analysisDir, "test*-analysis.json"))
        {
            try { result.AddRange(AnalysisExport.ReadJson(file)); }
            catch (JsonException) { /* skip malformed/foreign files */ }
        }
        return result;
    }

    private static List<IntegrityWarning> CollectIntegrityWarnings(IReadOnlyList<ResponseMarking> markings, Dictionary<string, AnalysisRecord> recordsById)
    {
        var warnings = new List<IntegrityWarning>();

        foreach (var m in markings)
        {
            if (m.Marked is null or MarkedWord.Hayir)
            {
                warnings.Add(new IntegrityWarning("NOT_MARKED", $"{m.Id}: Değerlendirildi is {(m.Marked is null ? "(blank)" : CodingSchemeText.Marked[m.Marked.Value].DropdownValue)}."));
            }

            // Revision 13 §4: this used to suppress on UYUM_YOK alone, which disagreed with §2's
            // two-condition exclusion rule -- a UYUM_YOK + KARSILADI response (in the measurement,
            // not excluded) had its blank sentences suppressed here without ever being flagged
            // anywhere, the same silent-denominator problem as §1, just for a warning instead of a
            // count. Excluded responses have nothing to warn about (their sentences are correctly
            // left blank); every response still in the measurement gets the usual check, including
            // the case the protocol has not yet ruled on (UYUM_YOK without ALAKASIZ) -- that warning
            // is not a defect report, it is a marker for the researcher to decide, not something to
            // silently assume away.
            if (!HasNoEvaluableContent(m))
            {
                foreach (var s in m.Sentences.Where(s => s.VerdictBlank))
                {
                    // Revision 6 §10.2: for a KISMEN (partial) response, a blank verdict on an
                    // off-target-language sentence is expected, not a mistake -- the message says so,
                    // so this warning does not read as a hard defect on every KISMEN response.
                    warnings.Add(new IntegrityWarning("BLANK_SENTENCE_VERDICT",
                        $"{m.Id}: sentence {s.SentenceNo?.ToString(CultureInfo.InvariantCulture) ?? "?"} has no verdict "
                            + $"(language compliance is {(m.LanguageCompliance is { } lc ? CodingSchemeText.LanguageCompliance[lc].DropdownValue : "blank")}"
                            + (m.LanguageCompliance == LanguageComplianceWord.Kismen ? " -- legitimate if this sentence is not in the target language" : "") + ")."));
                }
            }

            foreach (var s in m.Sentences)
            {
                var sentenceLabel = $"{m.Id}: sentence {s.SentenceNo?.ToString(CultureInfo.InvariantCulture) ?? "?"}";

                // Revision 6 §10.1: a sentence marked C1 (correct) cannot also carry an error-coded
                // word -- but the reverse is legitimate (a sentence with no error words can still be
                // C2/C3 on word order alone), so only the C1-with-errors direction is checked. TRM is
                // deliberately excluded: it is not an error, so a C1 sentence may still contain it.
                if (s.Verdict == SentenceVerdict.Dogru)
                {
                    var errorWordCount = (s.WrongForm ?? 0) + (s.WrongWord ?? 0) + (s.Invented ?? 0) + (s.CodeMixing ?? 0);
                    if (errorWordCount > 0)
                    {
                        warnings.Add(new IntegrityWarning("SENTENCE_VERDICT_CONFLICT",
                            $"{sentenceLabel}: verdict is C1 (correct) but {errorWordCount.ToString(CultureInfo.InvariantCulture)} word(s) are counted under an error code (FRM/CHO/INV/MIX)."));
                    }
                }

                // Revision 6 §10.3: a whole-number validation stops typed entry from exceeding the
                // sentence's word count (§2), but not a pasted value -- so it is re-checked here too.
                var codedWordCount = (s.WrongForm ?? 0) + (s.WrongWord ?? 0) + (s.Invented ?? 0) + (s.CodeMixing ?? 0) + (s.TermChoice ?? 0);
                if (codedWordCount > (s.WordCount ?? 0))
                {
                    warnings.Add(new IntegrityWarning("WORD_COUNT_OVERFLOW",
                        $"{sentenceLabel}: FRM+CHO+INV+MIX+TRM ({codedWordCount.ToString(CultureInfo.InvariantCulture)}) exceeds the word count ({(s.WordCount ?? 0).ToString(CultureInfo.InvariantCulture)})."));
                }

                foreach (var column in s.NonNumericColumns)
                {
                    warnings.Add(new IntegrityWarning("NON_NUMERIC", $"{sentenceLabel}: column {column} holds text that is not a number (likely pasted in)."));
                }

                // §2: contact-language counts are MIX's breakdown by source language, so their total
                // must equal MIX itself -- a word can only come from one source language. Skip only
                // when nothing was entered on either side (§2: "K boş ve S de boş/sıfırsa uyarı üretilmez").
                var k = s.CodeMixing;
                var total = s.TotalContactLanguageWords;
                var bothBlank = k is null && total == 0;
                if (!bothBlank && (k ?? 0) != total)
                {
                    warnings.Add(new IntegrityWarning("CONTACT_LANGUAGE_MISMATCH",
                        $"{sentenceLabel}: code mixing (K) is {(k?.ToString(CultureInfo.InvariantCulture) ?? "blank")} but the contact-language total (N+O+P+Q+R) is {total.ToString(CultureInfo.InvariantCulture)}."));
                }

                // §4 item 4: data validation stops negative entry through Excel's own UI, but not a
                // pasted or externally edited value, so a negative count is treated as a real
                // integrity problem here, not silently folded into 0.
                foreach (var (fieldName, value) in new (string, int?)[]
                {
                    ("wrong form (H)", s.WrongForm), ("wrong word (I)", s.WrongWord), ("invented (J)", s.Invented),
                    ("code mixing (K)", s.CodeMixing), ("term choice (M)", s.TermChoice),
                    ("Kabardian (N)", s.KbdCount), ("Russian (O)", s.RusCount), ("Turkish (P)", s.TurCount),
                    ("English (Q)", s.EngCount), ("other (R)", s.OtherCount),
                })
                {
                    if (value is < 0)
                    {
                        warnings.Add(new IntegrityWarning("NEGATIVE_COUNT", $"{sentenceLabel}: {fieldName} is {value.Value.ToString(CultureInfo.InvariantCulture)}."));
                    }
                }
            }

            if (!recordsById.ContainsKey(m.Id.ToString()))
            {
                warnings.Add(new IntegrityWarning("KEY_NOT_IN_ANALYSIS", $"{m.Id}: not found in the analysis output for this test."));
            }

            if (m.Id.Question == 7 && m.Marked == MarkedWord.Evet && m.Question7Verdict is null)
            {
                warnings.Add(new IntegrityWarning("Q7_VERDICT_MISSING", $"{m.Id}: marked Değerlendirildi=evet but the Question 7 verdict is blank."));
            }
        }

        foreach (var group in markings.GroupBy(m => m.Id).Where(g => g.Count() > 1))
        {
            var distinctLevels = group.Select(g => (g.LanguageCompliance, g.Semantic, g.Marked)).Distinct().Count();
            if (distinctLevels > 1)
            {
                warnings.Add(new IntegrityWarning("DUPLICATE_KEY_CONFLICT", $"{group.Key}: appears {group.Count()} times with conflicting response-level values."));
            }
        }

        return warnings;
    }

    private static void WriteIntegrityWarnings(string path, List<IntegrityWarning> warnings)
    {
        var sb = new StringBuilder();
        sb.Append("kind\tdetail\n");
        foreach (var w in warnings) sb.Append($"{w.Kind}\t{Escape(w.Detail)}\n");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteToolCategoryTable(string path, IReadOnlyList<ResponseMarking> markings, Dictionary<string, AnalysisRecord> recordsById, int test)
    {
        // Revision 13 §1/§3.2: the denominator (word count, from the analysis records) and the
        // numerator (error counts, from the marked sentences) must be excluded from the same set of
        // responses -- matched by response key, since that is the only thing the two sides share. A
        // marking with no corresponding analysis record needs no special handling here: it already
        // contributes nothing to wordCountsByTool (which only ever sums recordsById), whether
        // excluded or not; CollectIntegrityWarnings' KEY_NOT_IN_ANALYSIS already reports it.
        var excludedIds = markings.Where(HasNoEvaluableContent).Select(m => m.Id.ToString()).ToHashSet(StringComparer.Ordinal);

        var wordCountsByTool = ToolRegistry.Tools.ToDictionary(
            t => t.Number,
            t => recordsById.Values.Where(r => r.Test == test && r.Tool == t.Number && !excludedIds.Contains(r.Id)).Sum(r => r.WordCount));

        var sb = new StringBuilder();
        sb.Append("tool\tfrm\tcho\tinv\tmix\ttrm\ttotal_errors\twords\terrors_per_100_words\n");

        foreach (var tool in ToolRegistry.Tools)
        {
            var sentences = markings.Where(m => m.Id.Tool == tool.Number && !HasNoEvaluableContent(m)).SelectMany(m => m.Sentences).ToList();
            var frm = sentences.Sum(s => s.WrongForm ?? 0);
            var cho = sentences.Sum(s => s.WrongWord ?? 0);
            var inv = sentences.Sum(s => s.Invented ?? 0);
            var mix = sentences.Sum(s => s.CodeMixing ?? 0);
            var trm = sentences.Sum(s => s.TermChoice ?? 0);
            var totalErrors = frm + cho + inv + mix;
            var totalWords = wordCountsByTool.GetValueOrDefault(tool.Number, 0);
            var per100 = totalWords == 0 ? 0.0 : totalErrors * 100.0 / totalWords;

            sb.Append(string.Join('\t', tool.Number, frm, cho, inv, mix, trm, totalErrors, totalWords, per100.ToString("0.####", CultureInfo.InvariantCulture))).Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteContactLanguagesTable(string path, IReadOnlyList<ResponseMarking> markings)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tkbd\trus\ttur\teng\tother\ttotal\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var sentences = markings.Where(m => m.Id.Tool == tool.Number).SelectMany(m => m.Sentences).ToList();
            var kbd = sentences.Sum(s => s.KbdCount ?? 0);
            var rus = sentences.Sum(s => s.RusCount ?? 0);
            var tur = sentences.Sum(s => s.TurCount ?? 0);
            var eng = sentences.Sum(s => s.EngCount ?? 0);
            var other = sentences.Sum(s => s.OtherCount ?? 0);
            sb.Append(string.Join('\t', tool.Number, kbd, rus, tur, eng, other, kbd + rus + tur + eng + other)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteToolQuestionTable(string path, IReadOnlyList<ResponseMarking> markings, Dictionary<string, AnalysisRecord> recordsById)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tquestion\tword_count\terror_word_count\tsentence_count\tcorrect\tfaulty_comprehensible\tfaulty_incomprehensible\texcluded\n");
        foreach (var m in markings.OrderBy(m => m.Id.Tool).ThenBy(m => m.Id.Question))
        {
            // Revision 13 §3.3: the real word count, even for an excluded response -- this table is
            // the raw, per-response breakdown, not the report's own filtered aggregate, so a reader
            // who does not accept the exclusion rule can compute their own total from it.
            var wordCount = recordsById.TryGetValue(m.Id.ToString(), out var record) ? record.WordCount : 0;
            var errorWords = m.Sentences.Sum(s => (s.WrongForm ?? 0) + (s.WrongWord ?? 0) + (s.Invented ?? 0) + (s.CodeMixing ?? 0));
            var correct = m.Sentences.Count(s => s.Verdict == SentenceVerdict.Dogru);
            var faultyComprehensible = m.Sentences.Count(s => s.Verdict == SentenceVerdict.YanlisAmaAnlasilir);
            var faultyIncomprehensible = m.Sentences.Count(s => s.Verdict == SentenceVerdict.YanlisVeAnlamsiz);
            var excluded = HasNoEvaluableContent(m) ? "true" : "false";
            sb.Append(string.Join('\t', m.Id.Tool, m.Id.Question, wordCount, errorWords, m.Sentences.Count, correct, faultyComprehensible, faultyIncomprehensible, excluded)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteExcludedResponsesTable(string path, IReadOnlyList<ResponseMarking> markings, Dictionary<string, AnalysisRecord> recordsById)
    {
        var sb = new StringBuilder();
        sb.Append("key\ttool\tquestion\tlanguage_compliance\tsemantic\tword_count\tsentence_count\n");
        foreach (var m in markings.Where(HasNoEvaluableContent).OrderBy(m => m.Id.Tool).ThenBy(m => m.Id.Question))
        {
            var wordCount = recordsById.TryGetValue(m.Id.ToString(), out var record) ? record.WordCount : 0;
            var languageCompliance = m.LanguageCompliance is { } lc ? CodingSchemeText.LanguageCompliance[lc].Code : "";
            var semantic = m.Semantic is { } sem ? CodingSchemeText.SemanticAdequacy[sem].Code : "";
            sb.Append(string.Join('\t', m.Id.ToString(), m.Id.Tool, m.Id.Question, languageCompliance, semantic, wordCount, m.Sentences.Count)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteSentenceVerdictDistribution(string path, IReadOnlyList<ResponseMarking> markings)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tcorrect\tfaulty_comprehensible\tfaulty_incomprehensible\tblank\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var sentences = markings.Where(m => m.Id.Tool == tool.Number).SelectMany(m => m.Sentences).ToList();
            sb.Append(string.Join('\t',
                tool.Number,
                sentences.Count(s => s.Verdict == SentenceVerdict.Dogru),
                sentences.Count(s => s.Verdict == SentenceVerdict.YanlisAmaAnlasilir),
                sentences.Count(s => s.Verdict == SentenceVerdict.YanlisVeAnlamsiz),
                sentences.Count(s => s.VerdictBlank))).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteLanguageComplianceDistribution(string path, IReadOnlyList<ResponseMarking> markings)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tfull\tpartial\tnone\tblank\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var responses = markings.Where(m => m.Id.Tool == tool.Number).ToList();
            sb.Append(string.Join('\t',
                tool.Number,
                responses.Count(r => r.LanguageCompliance == LanguageComplianceWord.Tam),
                responses.Count(r => r.LanguageCompliance == LanguageComplianceWord.Kismen),
                responses.Count(r => r.LanguageCompliance == LanguageComplianceWord.UyumYok),
                responses.Count(r => r.LanguageCompliance is null))).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteCopyProbeResult(string path, Dictionary<string, AnalysisRecord> recordsById, int test)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tresult\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var id = new ResponseId(test, tool.Number, 5).ToString();
            var result = recordsById.TryGetValue(id, out var record) ? record.CopyProbe ?? "(none)" : "(no analysis)";
            sb.Append(tool.Number.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(result).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteQuestion7VerdictResult(string path, IReadOnlyList<ResponseMarking> markings)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tverdict\n");
        foreach (var m in markings.Where(m => m.Id.Question == 7).OrderBy(m => m.Id.Tool))
        {
            var verdict = m.Question7Verdict is { } v ? CodingSchemeText.Question7Verdicts[v].Code : "(blank)";
            sb.Append(m.Id.Tool.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(verdict).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteCharacterLayerSummary(string path, Dictionary<string, AnalysisRecord> recordsById, int test)
    {
        var sb = new StringBuilder();
        sb.Append("tool\tcorrect_palochka\tlookalike_palochka\tpalochka_accuracy\tmixed_script_proper_noun\tmixed_script_interleaved\tlatin_in_cyrillic_total\n");
        foreach (var tool in ToolRegistry.Tools)
        {
            var records = recordsById.Values.Where(r => r.Test == test && r.Tool == tool.Number).ToList();
            var correct = records.Sum(r => r.CorrectPalochkaUpper + r.CorrectPalochkaLower);
            var lookalike = records.Sum(r => r.LookalikePalochka);
            var accuracy = (correct + lookalike) == 0 ? 0.0 : correct * 100.0 / (correct + lookalike);
            var properNoun = records.Sum(r => r.MixedScriptByWeight.GetValueOrDefault(nameof(MixedScriptWeight.ProperNounAffix), 0));
            var interleaved = records.Sum(r => r.MixedScriptByWeight.GetValueOrDefault(nameof(MixedScriptWeight.Interleaved), 0));
            var latinTotal = records.Sum(r => r.LatinInCyrillicBreakdown.Values.Sum());
            sb.Append(string.Join('\t', tool.Number, correct, lookalike, accuracy.ToString("0.##", CultureInfo.InvariantCulture), properNoun, interleaved, latinTotal)).Append('\n');
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static readonly (int A, int B, string Label)[] StabilityPairs = [(1, 2, "round1: Test1 vs Test2"), (3, 4, "round2: Test3 vs Test4"), (1, 3, "cross-round: Test1 vs Test3")];

    private static void WriteStability(string path, List<AnalysisRecord> allRecords)
    {
        var sb = new StringBuilder();
        sb.Append("pair\ttool\tquestion\tsimilarity\n");

        foreach (var (a, b, label) in StabilityPairs)
        {
            foreach (var tool in ToolRegistry.Tools)
            {
                for (var q = 1; q <= 7; q++)
                {
                    var recA = allRecords.FirstOrDefault(r => r.Test == a && r.Tool == tool.Number && r.Question == q);
                    var recB = allRecords.FirstOrDefault(r => r.Test == b && r.Tool == tool.Number && r.Question == q);
                    if (recA is null || recB is null) continue;
                    var similarity = LevenshteinSimilarity.NormalizedSimilarity(recA.RepairedText, recB.RepairedText);
                    sb.Append(string.Join('\t', label, tool.Number, q, similarity.ToString("0.####", CultureInfo.InvariantCulture))).Append('\n');
                }
            }
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteHumanSummary(string path, int test, IReadOnlyList<ResponseMarking> markings, Dictionary<string, AnalysisRecord> recordsById, List<IntegrityWarning> warnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{AppInfo.DisplayName} {AppInfo.Version} -- report for Test {test}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Responses in workbook: {markings.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Responses matched to analysis output: {markings.Count(m => recordsById.ContainsKey(m.Id.ToString()))}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Responses excluded (no evaluable content): {markings.Count(HasNoEvaluableContent)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Data integrity warnings: {warnings.Count}");
        foreach (var group in warnings.GroupBy(w => w.Kind))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {group.Key}: {group.Count()}");
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string Escape(string s) => s.Replace('\t', ' ').Replace('\n', ' ');
}
