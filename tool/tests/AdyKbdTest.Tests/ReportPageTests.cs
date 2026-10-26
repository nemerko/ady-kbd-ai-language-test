using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 18: <c>akt summary</c> writes the results page (<c>report.html</c>,
/// <c>report.tr.html</c>, <c>report.ru.html</c>) and <c>akt report</c> writes
/// <c>test{n}-notes.tsv</c>. The page is self-contained, its numbers come from the tool's own
/// tables, and the evaluators' notes appear on it exactly as written.
/// </summary>
[TestClass]
public sealed class ReportPageTests
{
    private const string StandardNote = "Yazı sistemi karışıklığı.";
    // Sorts before StandardNote although it occurs less often (two rows against three).
    private const string EarlierStandardNote = "Cümle bölünme hatası: başlangıç.";
    private const string ResponseNote ="Gemini <b>reddetti</b> & 'ret' \"yazdı\".";
    private const string OneOffNote = "<i>tek</i> not & ikinci\tsütun\nikinci satır \\ ters";

    // The excluded response of the fixture: tool 1's answer to S4.
    private static readonly ResponseId Excluded = new(1, 1, 4);

    private sealed record Fixture(string Dir, string ReportDir, string[] NoteResponseKeys);

    // Real Test 1 data, every sentence labelled C1 with one error word (so the scores are not all 100),
    // one excluded response, two response notes and four sentence notes (three identical, one unique).
    private static void Run(Action<Fixture> assert)
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 1);
        var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
        var records = new List<AnalysisRecord>();
        foreach (var r in resolved)
        {
            foreach (var q in r.Selected.Questions)
            {
                var id = new ResponseId(1, r.Selected.Tool, q.Number);
                var a = TextAnalyzer.Analyze(q.AnswerText, profile);
                analyzed[id] = a;
                records.Add(AnalysisRecord.From(id, a));
            }
        }

        var dir = Path.Combine(Path.GetTempPath(), $"akt-rev18-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var workbookPath = Path.Combine(dir, "test1-marking.xlsx");
        try
        {
            AnalysisExport.WriteJson(Path.Combine(dir, "test1-analysis.json"), records);
            WorkbookBuilder.Build(workbookPath, new WorkbookInput(1, resolved, analyzed));

            var noteKeys = new[] { new ResponseId(1, 2, 1).ToString(), new ResponseId(1, 3, 2).ToString() };
            using (var document = SpreadsheetDocument.Open(workbookPath, true))
            {
                var workbookPart = document.WorkbookPart!;
                WorksheetPart Part(string name) => (WorksheetPart)workbookPart.GetPartById(workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == name).Id!);

                var responses = Part(WorkbookBuilder.ResponsesSheetName);
                foreach (var row in responses.Worksheet.Descendants<SheetData>().First().Elements<Row>().Where(r => r.RowIndex!.Value > 1))
                {
                    var key = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), null);
                    if (key == Excluded.ToString())
                    {
                        SetInline(row,
                            ("I", CodingSchemeText.LanguageCompliance[LanguageComplianceWord.UyumYok].DropdownValue),
                            ("J", CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Alakasiz].DropdownValue));
                    }
                    if (noteKeys.Contains(key)) SetInline(row, ("M", ResponseNote));
                }
                responses.Worksheet.Save();

                var sentences = Part(WorkbookBuilder.SentencesSheetName);
                var noted = 0;
                foreach (var row in sentences.Worksheet.Descendants<SheetData>().First().Elements<Row>().Where(r => r.RowIndex!.Value > 1))
                {
                    if (WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "A"), null) == Excluded.ToString()) continue;
                    SetInline(row, ("G", CodingSchemeText.SentenceVerdicts[SentenceVerdict.Dogru].DropdownValue), ("H", "1"));
                    if (noted < 3) SetInline(row, ("T", StandardNote));
                    else if (noted == 3) SetInline(row, ("T", OneOffNote));
                    else if (noted < 6) SetInline(row, ("T", EarlierStandardNote));
                    noted++;
                }
                sentences.Worksheet.Save();
            }

            ReportBuilder.Build(workbookPath, dir, dir);
            SummaryBuilder.Build(dir, dir);
            var reportDir = Path.Combine(dir, "report");
            ReportPageBuilder.Build(dir, dir, reportDir);
            assert(new Fixture(dir, reportDir, noteKeys));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void SetInline(Row row, params (string Column, string Value)[] values)
    {
        foreach (var (column, value) in values)
        {
            var cell = row.Elements<Cell>().Single(c => WorkbookXmlHelpers.ColumnOf(c.CellReference?.Value) == column);
            cell.DataType = CellValues.InlineString;
            cell.CellValue = null;
            cell.InlineString = new InlineString(new Text(value));
        }
    }

    private static string Page(Fixture f, string file) => File.ReadAllText(Path.Combine(f.ReportDir, file));

    private static string F1(string tsvValue, char separator) =>
        double.Parse(tsvValue, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', separator);

    // §5.1
    [TestMethod]
    public void ThreePages_AreWritten_WithTheRightLanguageAndCharset()
    {
        Run(f =>
        {
            foreach (var (file, lang) in new[] { ("report.html", "en"), ("report.tr.html", "tr"), ("report.ru.html", "ru") })
            {
                var html = Page(f, file);
                StringAssert.Contains(html, $"<html lang=\"{lang}\">");
                StringAssert.Contains(html, "<meta charset=\"utf-8\">");
            }
        });
    }

    // §5.2: the embedded data is the tool's own table, row for row.
    [TestMethod]
    public void EmbeddedJson_CarriesSummaryScoresExactly()
    {
        Run(f =>
        {
            var html = Page(f, "report.html");
            var json = Regex.Match(html, "<script type=\"application/json\" id=\"data\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
            using var doc = JsonDocument.Parse(json);
            var rows = doc.RootElement.GetProperty("summary_scores").GetProperty("rows").EnumerateArray().ToList();

            var tsv = File.ReadAllLines(Path.Combine(f.Dir, "summary-scores.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
            Assert.AreEqual(tsv.Count, rows.Count);
            foreach (var line in tsv)
            {
                var row = rows.Single(r => r.GetProperty("tool").GetString() == line[0]);
                Assert.AreEqual(line[^3], row.GetProperty("score").GetString());
                Assert.AreEqual(line[^2], row.GetProperty("rank").GetString());
                Assert.AreEqual(line[^1], row.GetProperty("tie").GetString());
            }
            Assert.IsTrue(doc.RootElement.GetProperty("test_scores").TryGetProperty("1", out _));
            Assert.AreEqual(25, doc.RootElement.GetProperty("summary_task_type").GetProperty("rows").GetArrayLength());
            Assert.IsFalse(json.Contains('<'), "the data block cannot end its own script element");
        });
    }

    // §5.3: the score card is plain HTML, with a decimal comma in Turkish and Russian and a point in English.
    [TestMethod]
    public void ScoreCard_IsWrittenIntoTheHtml_WithTheLanguagesDecimalSeparator()
    {
        Run(f =>
        {
            var tsv = File.ReadAllLines(Path.Combine(f.Dir, "summary-scores.tsv")).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
            foreach (var (file, separator) in new[] { ("report.html", '.'), ("report.tr.html", ','), ("report.ru.html", ',') })
            {
                var html = Page(f, file);
                var table = html[html.IndexOf("<table id=\"karne\">", StringComparison.Ordinal)..html.IndexOf("</table>", StringComparison.Ordinal)];
                foreach (var line in tsv)
                {
                    // columns: tool, test1, score, rank, tie
                    StringAssert.Contains(table, $">{F1(line[1], separator)}</td>", $"{file}: test score of tool {line[0]}");
                    StringAssert.Contains(table, $"<b>{F1(line[2], separator)}</b>", $"{file}: overall score of tool {line[0]}");
                    StringAssert.Contains(html, $"<div class=\"sc\">{F1(line[2], separator)}</div>", $"{file}: ranking score of tool {line[0]}");
                }
            }
            Assert.AreEqual("80,7", F1("80.6508", ','));
            Assert.AreEqual("80.7", F1("80.6508", '.'));
        });
    }

    // §5.3a
    [TestMethod]
    public void EveryInterfaceString_HasAllThreeLanguages_AndRussianCategoryWordsAreTheWorkbooksOwn()
    {
        foreach (var (key, texts) in ReportStrings.All)
        {
            foreach (var lang in ReportStrings.Languages)
            {
                Assert.IsTrue(texts.TryGetValue(lang.Code, out var text) && !string.IsNullOrWhiteSpace(text), $"{key}/{lang.Code} is empty");
            }
        }

        Run(f =>
        {
            var ru = Page(f, "report.ru.html");
            StringAssert.Contains(ru, CodingSchemeText.LanguageCompliance[LanguageComplianceWord.UyumYok].Ru);
            StringAssert.Contains(ru, CodingSchemeText.SemanticAdequacy[SemanticAdequacyWord.Alakasiz].Ru);
        });
    }

    // §5.4
    [TestMethod]
    public void ExcludedResponse_AndZeroScoreGroup_GetAutomaticFootnotes()
    {
        Run(f =>
        {
            var tr = Page(f, "report.tr.html");
            StringAssert.Contains(tr, "ChatGPT (OpenAI), Test 1, S4: ölçüm dışı — dil uyumu uyum yok (UYUM_YOK), anlam alakasız (ALAKASIZ) → 0 puan");
            StringAssert.Contains(tr, "ChatGPT (OpenAI), Test 1, S4 grubu: S4 puanı 0");
            StringAssert.Contains(Page(f, "report.html"), "ChatGPT (OpenAI), Test 1, S4: outside the measurement");
        });
    }

    // §5.5
    [TestMethod]
    public void NotesFile_CarriesTheWorkbookNotesExactly_AndThePageShowsThemEscaped()
    {
        Run(f =>
        {
            var notes = NotesTsv.Read(Path.Combine(f.Dir, "test1-notes.tsv"));

            var responseNotes = notes.Where(n => n.Sheet == "Responses").ToList();
            CollectionAssert.AreEquivalent(f.NoteResponseKeys, responseNotes.Select(n => n.Key).ToArray());
            Assert.IsTrue(responseNotes.All(n => n.Note == ResponseNote && n.SentenceNo is null));

            var sentenceNotes = notes.Where(n => n.Sheet == "Sentences").ToList();
            Assert.AreEqual(3, sentenceNotes.Count(n => n.Note == StandardNote));
            Assert.AreEqual(1, sentenceNotes.Count(n => n.Note == OneOffNote), "tab, line break, backslash and markup survive unchanged");
            Assert.IsTrue(sentenceNotes.All(n => n.SentenceNo is not null));

            var html = Page(f, "report.tr.html");
            StringAssert.Contains(html, "Gemini &lt;b&gt;reddetti&lt;/b&gt; &amp; &#39;ret&#39; &quot;yazdı&quot;.");
            Assert.IsFalse(html.Contains("Gemini <b>reddetti", StringComparison.Ordinal), "a note's markup is shown as text");
            StringAssert.Contains(html, "&lt;i&gt;tek&lt;/i&gt; not &amp; ikinci\tsütun\nikinci satır \\ ters");

            // the standard note is counted per tool and test (three rows, all tool 1 / test 1)
            var standard = html[html.IndexOf("class=\"stdnote\"", StringComparison.Ordinal)..];
            StringAssert.Contains(standard, StandardNote);
            StringAssert.Contains(standard, "<td class=\"n\"><b>Toplam</b></td><td><b>3</b></td><td><b>3</b></td>");
            // and the one-off is quoted with its key and sentence number, not counted
            Assert.IsTrue(Regex.IsMatch(html, @"T1-A\d-S\d · cümle \d+</b> <span class=""nt"">&lt;i&gt;tek"));
        });
    }

    // Revision 21 §2
    [TestMethod]
    public void StandardNoteTables_AreInAlphabeticalOrderOfTheNoteText_OnEveryPage()
    {
        Run(f =>
        {
            foreach (var file in new[] { "report.html", "report.tr.html", "report.ru.html" })
            {
                var html = Page(f, file);
                var earlier = html.IndexOf($"<div class=\"nt\">{EarlierStandardNote}</div>", StringComparison.Ordinal);
                var later = html.IndexOf($"<div class=\"nt\">{StandardNote}</div>", StringComparison.Ordinal);
                Assert.IsTrue(earlier >= 0 && later >= 0, $"{file}: both standard notes are listed");
                Assert.IsTrue(earlier < later, $"{file}: the alphabetically earlier note comes first although it is rarer");
            }
        });
    }

    [TestMethod]
    public void NoteEscaping_RoundTrips()
    {
        const string tricky = "a\\b\tc\nd\r\ne\\n literal";
        Assert.AreEqual(tricky, NotesTsv.Unescape(NotesTsv.Escape(tricky)));
        Assert.IsFalse(NotesTsv.Escape(tricky).Contains('\n'));
        Assert.IsFalse(NotesTsv.Escape(tricky).Contains('\t'));
    }

    // §5.6
    [TestMethod]
    public void Pages_LoadNothingFromOutside()
    {
        Run(f =>
        {
            foreach (var file in new[] { "report.html", "report.tr.html", "report.ru.html" })
            {
                var html = Page(f, file);
                Assert.IsFalse(Regex.IsMatch(html, @"(src|href)\s*=\s*[""']?\s*(https?:)?//", RegexOptions.IgnoreCase), file);
                Assert.IsFalse(html.Contains("<link", StringComparison.OrdinalIgnoreCase), file);
                Assert.IsFalse(html.Contains("@import", StringComparison.OrdinalIgnoreCase), file);
                Assert.IsFalse(html.Contains("url(", StringComparison.OrdinalIgnoreCase), file);
            }
        });
    }

    // §5.7
    [TestMethod]
    public void WithoutSummaryScores_NoPageIsProduced()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"akt-rev18-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(() => ReportPageBuilder.Build(dir, dir, Path.Combine(dir, "report")));
            StringAssert.Contains(ex.Message, "summary-scores.tsv");
            Assert.IsFalse(Directory.Exists(Path.Combine(dir, "report")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
