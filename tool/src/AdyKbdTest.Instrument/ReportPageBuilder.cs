using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdyKbdTest.Instrument;

/// <summary>
/// The results page (revision 18): <c>report.html</c>, <c>report.tr.html</c>, <c>report.ru.html</c>.
/// One self-contained file per language -- no external resource of any kind, inline SVG, a
/// <c>system-ui</c> font stack -- built by plain string assembly (no template engine, no new
/// package). Every number on the page comes from the tool's own tables
/// (<c>summary-scores.tsv</c>, <c>test{n}-scores.tsv</c>, <c>summary-task-type.tsv</c>,
/// <c>test{n}-tool-x-question.tsv</c>, <c>test{n}-excluded-responses.tsv</c>,
/// <c>test{n}-notes.tsv</c>) and the same rows are embedded in the page as JSON, so a reader who
/// saves the page has the data too. Sections 1 and 2 are written into the HTML itself; the script
/// only draws section 3 and the tooltips.
/// </summary>
public static class ReportPageBuilder
{
    private sealed record Table(IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows)
    {
        public string Cell(string[] row, string column) => row[Columns.ToList().IndexOf(column)];
    }

    private sealed record ZeroGroup(int Test, int Tool, QuestionGroup Group, IReadOnlyList<int> ZeroQuestions);

    private sealed class Model
    {
        public required List<int> Tests { get; init; }
        public required Table SummaryScores { get; init; }
        public required Dictionary<int, Table> TestScores { get; init; }
        public required Table TaskType { get; init; }
        public required List<(int Test, string[] Row)> Excluded { get; init; }
        public required List<ZeroGroup> ZeroGroups { get; init; }
        public required List<(int Test, NoteRow Note)> Notes { get; init; }
        public required List<(string File, string Hash)> Sources { get; init; }
    }

    /// <summary>
    /// Writes the three pages into <paramref name="reportOutDir"/> and returns their paths.
    /// <paramref name="analysisDir"/> holds the per-test files <c>akt report</c> wrote,
    /// <paramref name="summaryDir"/> the summary files <c>akt summary</c> wrote. Without
    /// <c>summary-scores.tsv</c> there is no page.
    /// </summary>
    public static IReadOnlyList<string> Build(string analysisDir, string summaryDir, string reportOutDir)
    {
        var model = Load(analysisDir, summaryDir);
        Directory.CreateDirectory(reportOutDir);

        var written = new List<string>();
        foreach (var language in ReportStrings.Languages)
        {
            var path = Path.Combine(reportOutDir, language.FileName);
            File.WriteAllText(path, Render(model, language), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(path);
        }
        return written;
    }

    // ---- loading ------------------------------------------------------------------------------

    private static Model Load(string analysisDir, string summaryDir)
    {
        var sources = new List<(string File, string Hash)>();

        string Existing(string dir, string name, bool required)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                sources.Add((name, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()[..12]));
                return path;
            }
            if (required) throw new InvalidOperationException($"{name} not found in {dir}; the report page is not produced without it. Run `akt report` for every test and then `akt summary`.");
            return "";
        }

        Table ReadTable(string path)
        {
            var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
            return new Table(lines[0].Split('\t'), lines.Skip(1).Select(l => l.Split('\t')).ToList());
        }

        var summaryScores = ReadTable(Existing(summaryDir, "summary-scores.tsv", required: true));
        var tests = summaryScores.Columns.Where(c => c.StartsWith("test", StringComparison.Ordinal))
            .Select(c => int.Parse(c[4..], CultureInfo.InvariantCulture)).ToList();
        var taskType = ReadTable(Existing(summaryDir, "summary-task-type.tsv", required: true));

        var testScores = new Dictionary<int, Table>();
        var excluded = new List<(int, string[])>();
        var zeroGroups = new List<ZeroGroup>();
        var notes = new List<(int, NoteRow)>();
        foreach (var test in tests)
        {
            testScores[test] = ReadTable(Existing(analysisDir, $"test{test}-scores.tsv", required: true));
            foreach (var row in ReadTable(Existing(analysisDir, $"test{test}-excluded-responses.tsv", required: true)).Rows) excluded.Add((test, row));

            // A group is "zero" if any of its questions scored 0 -- an excluded response, or one whose
            // every evaluated word is an error. Recomputed with the score rule from the question table.
            var questionTable = ReadTable(Existing(analysisDir, $"test{test}-tool-x-question.tsv", required: true));
            var questionScores = questionTable.Rows.Select(f => (
                Tool: int.Parse(f[0], CultureInfo.InvariantCulture),
                Question: int.Parse(f[1], CultureInfo.InvariantCulture),
                Score: ScoreRule.QuestionScore(int.Parse(f[4], CultureInfo.InvariantCulture), int.Parse(f[3], CultureInfo.InvariantCulture), f[^1] == "true"))).ToList();
            foreach (var tool in ToolRegistry.Tools)
            {
                foreach (var group in QuestionGroups.TaskTypes)
                {
                    var zero = questionScores.Where(q => q.Tool == tool.Number && group.Questions.Contains(q.Question) && q.Score == 0.0)
                        .Select(q => q.Question).OrderBy(q => q).ToList();
                    if (zero.Count > 0) zeroGroups.Add(new ZeroGroup(test, tool.Number, group, zero));
                }
            }

            var notesPath = Existing(analysisDir, $"test{test}-notes.tsv", required: false);
            if (notesPath.Length > 0) notes.AddRange(NotesTsv.Read(notesPath).Select(n => (test, n)));
        }

        return new Model
        {
            Tests = tests,
            SummaryScores = summaryScores,
            TestScores = testScores,
            TaskType = taskType,
            Excluded = excluded,
            ZeroGroups = zeroGroups,
            Notes = notes,
            Sources = sources,
        };
    }

    // ---- rendering ----------------------------------------------------------------------------

    private static string Enc(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
         .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal)
         .Replace("'", "&#39;", StringComparison.Ordinal);

    private static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private static double Dbl(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static string Render(Model m, ReportLanguage lang)
    {
        string T(string key) => ReportStrings.Get(key, lang.Code);
        string Fill(string key, params (string, string)[] values) => ReportStrings.Fill(T(key), values);
        string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', lang.DecimalSeparator);
        string ToolName(int tool) => ToolRegistry.NameOf(tool);
        string GroupLabel(QuestionGroup g) => g.QuestionsLabel.Replace('-', '–');
        string CategoryWord(CodeEntry e) => lang.Code switch { "tr" => e.Tr, "ru" => e.Ru, _ => e.En };
        string Category<TEnum>(IReadOnlyDictionary<TEnum, CodeEntry> map, string code) where TEnum : struct, Enum =>
            map.Values.FirstOrDefault(e => e.Code == code) is { } entry ? $"{CategoryWord(entry)} ({code})" : code;

        var nTests = m.Tests.Count.ToString(CultureInfo.InvariantCulture);
        var tools = ToolRegistry.Tools.Select(t => t.Number).ToList();

        // summary-scores.tsv: tool, test.., score, rank, tie
        var overall = tools.ToDictionary(t => t, t =>
        {
            var row = m.SummaryScores.Rows.Single(r => r[0] == t.ToString(CultureInfo.InvariantCulture));
            return (
                Score: Dbl(m.SummaryScores.Cell(row, "score")),
                Rank: int.Parse(m.SummaryScores.Cell(row, "rank"), CultureInfo.InvariantCulture),
                Tie: m.SummaryScores.Cell(row, "tie") == "true",
                Tests: m.Tests.ToDictionary(n => n, n => Dbl(m.SummaryScores.Cell(row, $"test{n}"))));
        });
        var order = tools.OrderBy(t => overall[t].Rank).ToList();
        var mean = overall.Values.Average(o => o.Score);

        // test{n}-scores.tsv: tool, q1_3, q4, q5, q6_7, score, rank, tie
        double TestGroup(int test, int tool, string group)
        {
            var table = m.TestScores[test];
            return Dbl(table.Cell(table.Rows.Single(r => r[0] == tool.ToString(CultureInfo.InvariantCulture)), group));
        }

        var h = new StringBuilder();
        h.Append("<!doctype html>\n<html lang=\"").Append(lang.HtmlLang).Append("\">\n<head>\n<meta charset=\"utf-8\">\n");
        h.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        h.Append("<title>").Append(Enc(T("page_title"))).Append("</title>\n<style>\n").Append(Css).Append("</style>\n</head>\n<body>\n<main>\n");
        h.Append("  <h1>").Append(Enc(T("page_title"))).Append("</h1>\n");
        h.Append("  <p class=\"sub\">").Append(Enc(T("intro"))).Append("</p>\n\n");

        // 1. overall ranking -- written into the HTML, bars included
        h.Append("  <h2>").Append(Enc(Fill("h_ranking", ("n", nTests)))).Append("</h2>\n  <div class=\"card\" id=\"ranking\">\n");
        foreach (var t in order)
        {
            var o = overall[t];
            h.Append("    <div class=\"rankrow\"><div class=\"rk\">").Append(o.Rank.ToString(CultureInfo.InvariantCulture)).Append(".</div>");
            h.Append("<div><div class=\"nm\">").Append(Enc(ToolName(t))).Append("</div></div><div>");
            h.Append("<div class=\"bar\"><i style=\"width:").Append(Num(o.Score)).Append("%\"></i><b style=\"left:").Append(Num(mean))
             .Append("%\" title=\"").Append(Enc(Fill("mean_tip", ("x", F1(mean))))).Append("\"></b></div>");
            h.Append("<div class=\"mini\">").Append(Enc(T("tests_label"))).Append(": ")
             .Append(string.Join(" · ", m.Tests.Select(n => F1(o.Tests[n])))).Append("</div></div>");
            h.Append("<div class=\"sc\">").Append(F1(o.Score)).Append("</div></div>\n");
        }
        h.Append("  </div>\n\n");

        // 2. score card -- a real table
        h.Append("  <h2>").Append(Enc(T("h_card"))).Append("</h2>\n  <p class=\"sub\">").Append(Enc(T("card_note"))).Append("</p>\n");
        h.Append("  <div class=\"card\"><div class=\"tablewrap\"><table id=\"karne\">\n    <tr><th>").Append(Enc(T("th_tool"))).Append("</th>");
        foreach (var n in m.Tests) h.Append("<th>").Append(Enc(Fill("th_test", ("n", n.ToString(CultureInfo.InvariantCulture))))).Append("</th>");
        h.Append("<th>").Append(Enc(T("th_overall"))).Append("</th><th>").Append(Enc(T("th_rank"))).Append("</th></tr>\n");
        foreach (var t in order)
        {
            h.Append("    <tr><td class=\"n\">").Append(Enc(ToolName(t))).Append("</td>");
            foreach (var n in m.Tests)
            {
                var v = overall[t].Tests[n];
                var groupScores = QuestionGroups.TaskTypes.Select(g => (Group: g, Score: TestGroup(n, t, g.Code))).ToList();
                var zero = groupScores.Any(g => g.Score == 0.0);
                var tip = $"<b>{Enc(ToolName(t))} · {Enc(Fill("th_test", ("n", n.ToString(CultureInfo.InvariantCulture))))}</b><br>"
                          + string.Join("<br>", groupScores.Select(g => $"{GroupLabel(g.Group)}: {F1(g.Score)}"));
                h.Append("<td class=\"").Append(Band(v)).Append(zero ? " zero" : "").Append("\" data-tip=\"").Append(Enc(tip)).Append("\">").Append(F1(v)).Append("</td>");
            }
            h.Append("<td class=\"").Append(Band(overall[t].Score)).Append("\"><b>").Append(F1(overall[t].Score)).Append("</b></td><td>")
             .Append(overall[t].Rank.ToString(CultureInfo.InvariantCulture)).Append("</td></tr>\n");
        }
        h.Append("  </table></div>\n    <div class=\"scale\">").Append(Enc(T("scale_low")))
         .Append(" <i style=\"background:var(--seq-1)\"></i><i style=\"background:var(--seq-2)\"></i><i style=\"background:var(--seq-3)\"></i><i style=\"background:var(--seq-4)\"></i> ")
         .Append(Enc(T("scale_high"))).Append(' ').Append(Enc(T("scale_bands"))).Append("</div>\n  </div>\n\n");

        // 3. deviation from the five-tool mean -- drawn by the script from the embedded data
        h.Append("  <h2>").Append(Enc(T("h_dev"))).Append("</h2>\n  <p class=\"sub\">").Append(Enc(Fill("dev_note", ("n", nTests)))).Append("</p>\n");
        h.Append("  <div class=\"devgrid\" id=\"dev\"></div>\n  <noscript><p class=\"sub\">").Append(Enc(T("noscript"))).Append("</p></noscript>\n\n");

        // footnotes: derived from the tables
        h.Append("  <h2>").Append(Enc(T("h_footnotes"))).Append("</h2>\n  <ul class=\"notes\">\n");
        var footnotes = Footnotes(m, lang, T, ToolName, GroupLabel, Category, F1);
        if (footnotes.Count == 0) footnotes.Add(T("fn_none"));
        foreach (var f in footnotes) h.Append("    <li>").Append(Enc(f)).Append("</li>\n");
        h.Append("  </ul>\n\n");

        AppendEvaluatorNotes(h, m, lang, T, ToolName);

        // source line
        h.Append("  <div class=\"src\">").Append(Enc(T("src_label"))).Append(": ")
         .Append(string.Join(", ", m.Sources.Select(s => $"<code>{Enc(s.File)}</code> ({s.Hash})")))
         .Append("; akt ").Append(AppInfo.Version).Append("; ").Append(Enc(T("src_generated"))).Append(' ')
         .Append(DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        h.Append("</div>\n</main>\n<div id=\"tip\" role=\"tooltip\"></div>\n");

        h.Append("<script type=\"application/json\" id=\"data\">").Append(DataJson(m, lang, T)).Append("</script>\n");
        h.Append("<script>\n").Append(Script).Append("</script>\n</body>\n</html>\n");
        return h.ToString();
    }

    private static string Band(double v) => v >= 85 ? "b4" : v >= 80 ? "b3" : v >= 70 ? "b2" : "b1";

    private static List<string> Footnotes(
        Model m, ReportLanguage lang, Func<string, string> t, Func<int, string> toolName, Func<QuestionGroup, string> groupLabel,
        Func<IReadOnlyDictionary<LanguageComplianceWord, CodeEntry>, string, string> languageCategory, Func<double, string> f1)
    {
        var notes = new List<string>();

        // excluded responses (excluded-responses.tsv: key tool question language_compliance semantic word_count sentence_count)
        foreach (var (test, row) in m.Excluded.OrderBy(e => e.Test).ThenBy(e => int.Parse(e.Row[1], CultureInfo.InvariantCulture)).ThenBy(e => int.Parse(e.Row[2], CultureInfo.InvariantCulture)))
        {
            var lc = languageCategory(CodingSchemeText.LanguageCompliance, row[3]);
            var semEntry = CodingSchemeText.SemanticAdequacy.Values.FirstOrDefault(e => e.Code == row[4]);
            var sem = semEntry is null ? row[4] : $"{(lang.Code switch { "tr" => semEntry.Tr, "ru" => semEntry.Ru, _ => semEntry.En })} ({row[4]})";
            notes.Add(ReportStrings.Fill(t("fn_excluded"),
                ("tool", toolName(int.Parse(row[1], CultureInfo.InvariantCulture))),
                ("t", test.ToString(CultureInfo.InvariantCulture)),
                ("q", row[2]),
                ("lc", lc),
                ("sem", sem)));
        }

        // question groups containing a question that scored 0
        foreach (var z in m.ZeroGroups.OrderBy(z => z.Test).ThenBy(z => z.Tool).ThenBy(z => z.Group.Questions[0]))
        {
            notes.Add(ReportStrings.Fill(t("fn_zero"),
                ("tool", toolName(z.Tool)),
                ("t", z.Test.ToString(CultureInfo.InvariantCulture)),
                ("g", groupLabel(z.Group)),
                ("qs", string.Join(", ", z.ZeroQuestions.Select(q => $"S{q}")))));
        }

        // tied ranks -- overall, then each test
        void Ties(Table table, string scope)
        {
            var tied = table.Rows.Where(r => table.Cell(r, "tie") == "true").GroupBy(r => table.Cell(r, "score"));
            foreach (var g in tied)
            {
                notes.Add(ReportStrings.Fill(t("fn_tie"),
                    ("scope", scope),
                    ("tools", string.Join(", ", g.Select(r => toolName(int.Parse(r[0], CultureInfo.InvariantCulture))))),
                    ("s", f1(Dbl(g.Key)))));
            }
        }
        Ties(m.SummaryScores, t("scope_overall"));
        foreach (var test in m.Tests) Ties(m.TestScores[test], ReportStrings.Fill(t("scope_test"), ("t", test.ToString(CultureInfo.InvariantCulture))));

        return notes;
    }

    private static void AppendEvaluatorNotes(StringBuilder h, Model m, ReportLanguage lang, Func<string, string> t, Func<int, string> toolName)
    {
        string TestName(int n) => ReportStrings.Fill(t("th_test"), ("n", n.ToString(CultureInfo.InvariantCulture)));

        h.Append("  <h2>").Append(Enc(t("h_notes"))).Append("</h2>\n  <p class=\"sub\">").Append(Enc(t("notes_intro"))).Append("</p>\n");

        // Responses sheet notes, as written, tool -> test -> question
        h.Append("  <h3>").Append(Enc(t("h_notes_responses"))).Append("</h3>\n");
        var responseNotes = m.Notes.Where(n => n.Note.Sheet == "Responses").OrderBy(n => n.Note.Tool).ThenBy(n => n.Test).ThenBy(n => n.Note.Question).ToList();
        if (responseNotes.Count == 0) h.Append("  <p class=\"sub\">").Append(Enc(t("none"))).Append("</p>\n");
        else
        {
            h.Append("  <ul class=\"enotes\">\n");
            foreach (var (test, note) in responseNotes)
            {
                h.Append("    <li><b>").Append(Enc($"{toolName(note.Tool)} · {TestName(test)} · S{note.Question}")).Append("</b> <span class=\"nt\">")
                 .Append(Enc(note.Note)).Append("</span></li>\n");
            }
            h.Append("  </ul>\n");
        }

        // Sentences sheet notes: text that occurs on more than one row is a standard note (counted),
        // text that occurs on exactly one row is quoted with its key and sentence number.
        var sentenceNotes = m.Notes.Where(n => n.Note.Sheet == "Sentences").ToList();
        var byText = sentenceNotes.GroupBy(n => n.Note.Note, StringComparer.Ordinal).ToList();
        // Standard notes are listed alphabetically by their text (revision 21), not by count: the order depends on
        // the note, never on the page language. The ordinal tie-break only fixes the order of texts that differ in case alone.
        var byNoteText = Comparer<string>.Create((a, b) => CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase));
        var standard = byText.Where(g => g.Count() > 1).OrderBy(g => g.Key, byNoteText).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        var oneOff = byText.Where(g => g.Count() == 1).Select(g => g.Single())
            .OrderBy(n => n.Note.Tool).ThenBy(n => n.Test).ThenBy(n => n.Note.Question).ThenBy(n => n.Note.SentenceNo).ToList();

        h.Append("  <h3>").Append(Enc(t("h_notes_standard"))).Append("</h3>\n");
        if (standard.Count == 0) h.Append("  <p class=\"sub\">").Append(Enc(t("none"))).Append("</p>\n");
        foreach (var group in standard)
        {
            h.Append("  <div class=\"stdnote\"><div class=\"nt\">").Append(Enc(group.Key)).Append("</div><div class=\"tablewrap\"><table class=\"cnt\">\n    <tr><th>")
             .Append(Enc(t("th_tool"))).Append("</th>");
            foreach (var n in m.Tests) h.Append("<th>").Append(Enc(TestName(n))).Append("</th>");
            h.Append("<th>").Append(Enc(t("th_total"))).Append("</th></tr>\n");
            foreach (var tool in ToolRegistry.Tools)
            {
                var counts = m.Tests.Select(n => group.Count(x => x.Note.Tool == tool.Number && x.Test == n)).ToList();
                h.Append("    <tr><td class=\"n\">").Append(Enc(toolName(tool.Number))).Append("</td>")
                 .Append(string.Concat(counts.Select(c => $"<td>{c.ToString(CultureInfo.InvariantCulture)}</td>")))
                 .Append("<td>").Append(counts.Sum().ToString(CultureInfo.InvariantCulture)).Append("</td></tr>\n");
            }
            var totals = m.Tests.Select(n => group.Count(x => x.Test == n)).ToList();
            h.Append("    <tr><td class=\"n\"><b>").Append(Enc(t("th_total"))).Append("</b></td>")
             .Append(string.Concat(totals.Select(c => $"<td><b>{c.ToString(CultureInfo.InvariantCulture)}</b></td>")))
             .Append("<td><b>").Append(totals.Sum().ToString(CultureInfo.InvariantCulture)).Append("</b></td></tr>\n  </table></div></div>\n");
        }

        h.Append("  <h3>").Append(Enc(t("h_notes_oneoff"))).Append("</h3>\n");
        if (oneOff.Count == 0) h.Append("  <p class=\"sub\">").Append(Enc(t("none"))).Append("</p>\n");
        else
        {
            h.Append("  <ul class=\"enotes\">\n");
            foreach (var (test, note) in oneOff)
            {
                var where = $"{toolName(note.Tool)} · {note.Key} · {ReportStrings.Fill(t("sentence_no"), ("n", note.SentenceNo?.ToString(CultureInfo.InvariantCulture) ?? "?"))}";
                h.Append("    <li><b>").Append(Enc(where)).Append("</b> <span class=\"nt\">").Append(Enc(note.Note)).Append("</span></li>\n");
            }
            h.Append("  </ul>\n");
        }
        h.Append('\n');
    }

    private static string DataJson(Model m, ReportLanguage lang, Func<string, string> t)
    {
        object TableJson(Table table) => new
        {
            columns = table.Columns,
            rows = table.Rows.Select(r => table.Columns.Zip(r).ToDictionary(x => x.First, x => x.Second)).ToList(),
        };

        var data = new Dictionary<string, object?>
        {
            ["tests"] = m.Tests,
            ["tools"] = ToolRegistry.Tools.Select(x => new { n = x.Number, name = x.Name }).ToList(),
            ["groups"] = QuestionGroups.TaskTypes.Select(g => new { code = g.Code, label = g.QuestionsLabel.Replace('-', '–'), sub = t($"group_{g.Code}") }).ToList(),
            ["ui"] = new
            {
                dec = lang.DecimalSeparator.ToString(),
                dev_mean = t("dev_mean"),
                dev_tip_score = t("dev_tip_score"),
                tests_label = t("tests_label"),
            },
            ["summary_scores"] = TableJson(m.SummaryScores),
            ["test_scores"] = m.TestScores.ToDictionary(x => x.Key.ToString(CultureInfo.InvariantCulture), x => TableJson(x.Value)),
            ["summary_task_type"] = TableJson(m.TaskType),
            ["hashes"] = m.Sources.ToDictionary(s => s.File, s => s.Hash),
        };

        // The default encoder escapes <, >, & and every non-ASCII character, so the block can neither
        // end its own <script> element nor depend on the page encoding.
        return JsonSerializer.Serialize(data);
    }

    // ---- static page parts --------------------------------------------------------------------

    private const string Css = """
  :root {
    color-scheme: light;
    --surface-1: #fcfcfb; --surface-2: #f3f2ef;
    --text-primary: #0b0b0b; --text-secondary: #52514e; --text-muted: #7a7974;
    --grid: #e2e1dc; --axis: #b9b8b2;
    --pos: #2a78d6; --neg: #eb6834; --pos-soft: #d6e6f8; --neg-soft: #fbe0d4;
    --seq-1: #e8f0fb; --seq-2: #bcd4f1; --seq-3: #7faae3; --seq-4: #2a78d6;
  }
  @media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) {
      color-scheme: dark;
      --surface-1: #1a1a19; --surface-2: #232322;
      --text-primary: #f0efec; --text-secondary: #c3c2b7; --text-muted: #8f8e88;
      --grid: #33332f; --axis: #5a5a54;
      --pos: #3987e5; --neg: #d95926; --pos-soft: #1f3550; --neg-soft: #4a2a1c;
      --seq-1: #26303c; --seq-2: #2b4668; --seq-3: #2f6bb0; --seq-4: #3987e5;
    }
  }
  :root[data-theme="dark"] {
    color-scheme: dark;
    --surface-1: #1a1a19; --surface-2: #232322;
    --text-primary: #f0efec; --text-secondary: #c3c2b7; --text-muted: #8f8e88;
    --grid: #33332f; --axis: #5a5a54;
    --pos: #3987e5; --neg: #d95926; --pos-soft: #1f3550; --neg-soft: #4a2a1c;
    --seq-1: #26303c; --seq-2: #2b4668; --seq-3: #2f6bb0; --seq-4: #3987e5;
  }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--surface-1); color: var(--text-primary); font: 15px/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
  main { max-width: 1040px; margin: 0 auto; padding: 24px 16px 48px; }
  h1 { font-size: 22px; margin: 0 0 4px; }
  h2 { font-size: 17px; margin: 32px 0 6px; }
  h3 { font-size: 15px; margin: 18px 0 4px; }
  .sub { color: var(--text-secondary); margin: 0 0 8px; max-width: 760px; }
  .card { background: var(--surface-2); border-radius: 12px; padding: 16px; }
  /* 1. genel sıralama */
  .rankrow { display: grid; grid-template-columns: 28px 118px 1fr 52px; align-items: center; gap: 10px; padding: 7px 0; border-bottom: 1px solid var(--grid); }
  .rankrow:last-child { border-bottom: 0; }
  .rk { font-weight: 700; color: var(--text-secondary); text-align: right; }
  .nm { font-weight: 600; }
  .bar { height: 16px; background: var(--grid); border-radius: 0 4px 4px 0; position: relative; }
  .bar > i { display: block; position: absolute; left: 0; top: 0; bottom: 0; background: var(--pos); border-radius: 0 4px 4px 0; }
  .bar > b { display: block; position: absolute; top: -3px; width: 2px; height: 22px; background: var(--text-muted); }
  .sc { font-variant-numeric: tabular-nums; text-align: right; font-weight: 600; }
  .mini { color: var(--text-muted); font-size: 12px; margin-top: 2px; }
  /* 2. karne */
  .tablewrap { overflow-x: auto; }
  table { border-collapse: separate; border-spacing: 3px; font-size: 14px; min-width: 560px; }
  th { color: var(--text-secondary); font-weight: 600; text-align: center; padding: 4px 6px; }
  th:first-child { text-align: left; }
  td { padding: 8px 10px; text-align: center; border-radius: 6px; font-variant-numeric: tabular-nums; min-width: 64px; }
  td.n { text-align: left; font-weight: 600; background: none; padding-left: 0; }
  td.b1 { background: var(--seq-1); } td.b2 { background: var(--seq-2); } td.b3 { background: var(--seq-3); color: #fff; } td.b4 { background: var(--seq-4); color: #fff; }
  td.zero { outline: 2px dashed var(--neg); outline-offset: -2px; }
  .scale { display: flex; align-items: center; gap: 6px; margin-top: 10px; font-size: 12px; color: var(--text-secondary); flex-wrap: wrap; }
  .scale i { display: inline-block; width: 26px; height: 12px; border-radius: 3px; }
  /* 3. sapma */
  .devgrid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }
  @media (max-width: 640px) { .devgrid { grid-template-columns: 1fr; } }
  .dev { background: var(--surface-2); border-radius: 12px; padding: 12px 14px 8px; }
  .dev h3 { margin: 0; font-size: 15px; }
  .dev .m { font-size: 12px; color: var(--text-muted); margin-bottom: 6px; }
  .dev svg { width: 100%; height: auto; display: block; }
  .dl { font-size: 12px; fill: var(--text-primary); }
  .dv { font-size: 11px; fill: var(--text-secondary); font-variant-numeric: tabular-nums; }
  .zero-line { stroke: var(--axis); stroke-width: 1.5; }
  .tick { stroke: var(--grid); stroke-width: 1; }
  .tk { font-size: 10px; fill: var(--text-muted); }
  .notes { margin-top: 24px; color: var(--text-secondary); font-size: 13px; max-width: 820px; }
  .notes li { margin: 4px 0; }
  /* değerlendirici notları */
  .enotes { list-style: none; padding: 0; margin: 8px 0; font-size: 13px; max-width: 820px; }
  .enotes li { margin: 6px 0; overflow-wrap: anywhere; }
  .nt { white-space: pre-wrap; overflow-wrap: anywhere; }
  .stdnote { margin: 14px 0; max-width: 820px; }
  .stdnote > .nt { font-weight: 600; margin-bottom: 2px; }
  table.cnt { min-width: 0; font-size: 13px; }
  table.cnt td { min-width: 0; padding: 4px 10px; background: var(--surface-2); }
  table.cnt td.n { background: none; }
  .src { font-size: 12px; color: var(--text-muted); margin-top: 16px; overflow-wrap: anywhere; }
  code { font-size: 12px; }
  #tip { position: fixed; pointer-events: none; z-index: 10; display: none; background: var(--text-primary); color: var(--surface-1); padding: 6px 9px; border-radius: 6px; font-size: 12px; line-height: 1.35; max-width: 260px; }
  .hit { fill: transparent; }

""";

    private const string Script = """
const J = JSON.parse(document.getElementById('data').textContent);
const U = J.ui;
const f1 = x => x.toFixed(1).replace('.', U.dec);
const fs = x => (x > 0 ? '+' : (x < 0 ? '−' : '')) + Math.abs(x).toFixed(1).replace('.', U.dec);
const names = {}; J.tools.forEach(t => names[t.n] = t.name);
const tip = document.getElementById('tip');
const showTip = (ev, html) => { tip.innerHTML = html; tip.style.display = 'block'; moveTip(ev); };
const moveTip = ev => { tip.style.left = Math.min(ev.clientX + 14, innerWidth - tip.offsetWidth - 8) + 'px'; tip.style.top = Math.min(ev.clientY + 14, innerHeight - tip.offsetHeight - 8) + 'px'; };
const hideTip = () => tip.style.display = 'none';

// 2. karne: the table is already in the page; only the tooltips are attached here
document.querySelectorAll('td[data-tip]').forEach(td => { td.addEventListener('mouseenter', ev => showTip(ev, td.dataset.tip)); td.addEventListener('mousemove', moveTip); td.addEventListener('mouseleave', hideTip); });

// data: test -> tool -> group score (from test{n}-scores.tsv); group score over all tests = their mean
const order = J.summary_scores.rows.slice().sort((a, b) => parseFloat(a.rank) - parseFloat(b.rank)).map(r => parseInt(r.tool, 10));
const testgrp = {};
for (const tn of J.tests) { testgrp[tn] = {}; for (const r of J.test_scores[tn].rows) testgrp[tn][r.tool] = { q1_3: parseFloat(r.q1_3), q4: parseFloat(r.q4), q5: parseFloat(r.q5), q6_7: parseFloat(r.q6_7) }; }
const grp = {};
for (const k of order) { grp[k] = {}; for (const g of J.groups) grp[k][g.code] = J.tests.reduce((a, tn) => a + testgrp[tn][k][g.code], 0) / J.tests.length; }

// 3. sapma
const NS = 'http://www.w3.org/2000/svg';
const el = (n, a = {}, p) => { const e = document.createElementNS(NS, n); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const devgrid = document.getElementById('dev');
const W = 460, rowH = 24, left = Math.max(74, Math.ceil(Math.max(...J.tools.map(t => t.name.length)) * 6.6) + 10), right = 8, padTop = 18, half = (W - left - right) / 2, R = 32;
for (const G of J.groups) {
  const g = G.code, lab = G.label, sub = G.sub;
  const gm = order.reduce((a, k) => a + grp[k][g], 0) / order.length;
  const card = document.createElement('div'); card.className = 'dev';
  card.innerHTML = `<h3>${lab}</h3><div class="m">${sub} · ${U.dev_mean} ${f1(gm)}</div>`;
  const H = padTop + rowH * order.length + 4;
  const svg = el('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': `${lab}: ` + order.map(k => `${names[k]} ${fs(grp[k][g] - gm)}`).join(', ') });
  const cx = left + half;
  for (const v of [-R, -R / 2, R / 2, R]) { const x = cx + v / R * half; el('line', { class: 'tick', x1: x, y1: padTop - 4, x2: x, y2: H - 4 }, svg); const tk = el('text', { class: 'tk', x, y: padTop - 7, 'text-anchor': 'middle' }, svg); tk.textContent = (v > 0 ? '+' : '') + v; }
  el('line', { class: 'zero-line', x1: cx, y1: padTop - 4, x2: cx, y2: H - 4 }, svg);
  const sorted = [...order].sort((a, b) => (grp[b][g] - grp[a][g]));
  sorted.forEach((k, i) => {
    const dv = grp[k][g] - gm; const y = padTop + i * rowH;
    const w = Math.min(Math.abs(dv), R) / R * half;
    const nm = el('text', { class: 'dl', x: left - 8, y: y + 15, 'text-anchor': 'end' }, svg); nm.textContent = names[k];
    el('rect', { x: dv >= 0 ? cx : cx - w, y: y + 4, width: w, height: rowH - 8, rx: 3, fill: dv >= 0 ? 'var(--pos)' : 'var(--neg)' }, svg);
    const vl = el('text', { class: 'dv', x: dv >= 0 ? cx + w + 4 : cx - w - 4, y: y + 15, 'text-anchor': dv >= 0 ? 'start' : 'end' }, svg); vl.textContent = fs(dv);
    const hit = el('rect', { class: 'hit', x: 0, y, width: W, height: rowH }, svg);
    const html = `<b>${names[k]} · ${lab}</b><br>${U.dev_tip_score.replace('{s}', f1(grp[k][g])).replace('{m}', f1(gm))}<br>${U.tests_label}: ${J.tests.map(tn => f1(testgrp[tn][k][g])).join(' · ')}`;
    hit.addEventListener('mouseenter', ev => showTip(ev, html)); hit.addEventListener('mousemove', moveTip); hit.addEventListener('mouseleave', hideTip);
  });
  card.appendChild(svg); devgrid.appendChild(card);
}

""";
}
