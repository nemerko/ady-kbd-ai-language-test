using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 14: the tool must not show an evaluator a repaired version of a response. The repaired
/// copy is for counting only; the Sentences sheet shows the response's original characters (§3.1),
/// and repair no longer fires on the Latin part of a hyphenated "Latin abbreviation + Cyrillic
/// suffix" word such as "AI-компьютерым" (§3.2, §3.3). Nothing that is counted changes.
/// </summary>
[TestClass]
public sealed class DisplayTextTests
{
    private static AnalyzedResponse Analyze(string text) => TextAnalyzer.Analyze(text, TestProfiles.Ady);

    private static int Count(AnalyzedResponse a, string code) => a.Findings.Count(f => f.Code == code);

    // §4.1: a Latin abbreviation with a Cyrillic suffix is not a palochka look-alike.
    [TestMethod]
    public void LatinAbbreviationWithCyrillicSuffix_IsNotRepaired()
    {
        var a = Analyze("Сэ AI-компьютерым сыкъэуцущт.");

        Assert.AreEqual(0, Count(a, FindingCodes.PalochkaLookAlike));
        Assert.IsTrue(a.Sentences[0].Text.Contains("AI-компьютерым", StringComparison.Ordinal));
        Assert.IsTrue(a.Sentences[0].DisplayText.Contains("AI-компьютерым", StringComparison.Ordinal));
        Assert.AreEqual('A', a.Sentences[0].DisplayText[a.Sentences[0].DisplayText.IndexOf("AI-", StringComparison.Ordinal)]);

        var mixed = a.Findings.Single(f => f.Code == FindingCodes.MixedScript);
        Assert.AreEqual(MixedScriptWeight.ProperNounAffix, mixed.Weight);
        Assert.AreEqual(0, a.Metrics.LatinCharacterBreakdownInCyrillicTokens.Count);
    }

    // §4.2: a look-alike in a Cyrillic word is still counted and still repaired in the counting
    // copy, but the displayed sentence keeps what the tool actually wrote.
    [TestMethod]
    public void LookAlikeInCyrillicWord_IsRepairedForCounting_ButDisplayedOriginal()
    {
        var a = Analyze("Iофыгъу дэгъу.");

        var finding = a.Findings.Single(f => f.Code == FindingCodes.PalochkaLookAlike);
        Assert.AreEqual("I", finding.Found);
        Assert.AreEqual("Ӏ", finding.Expected);
        Assert.AreEqual("Ӏофыгъу дэгъу.", a.Sentences[0].Text);
        Assert.AreEqual("Iофыгъу дэгъу.", a.Sentences[0].DisplayText);
        Assert.AreEqual(2, a.Metrics.WordCount);
    }

    // §4.3: only the Cyrillic-dominant part of a hyphenated chunk is repaired.
    [TestMethod]
    public void HyphenatedChunk_RepairsOnlyTheCyrillicDominantPart()
    {
        var a = Analyze("Сэ сы-ИИ-щIэныгъэ-бот.");

        Assert.AreEqual(1, Count(a, FindingCodes.PalochkaLookAlike));
        Assert.IsTrue(a.Sentences[0].DisplayText.Contains("щIэныгъэ", StringComparison.Ordinal), "the Latin I must still be shown");
        Assert.IsTrue(a.Sentences[0].Text.Contains("щӀэныгъэ", StringComparison.Ordinal));
    }

    // §4.4: a Latin-dominant stem is still left alone, and nothing differs between the two texts.
    [TestMethod]
    public void GoogleSuffix_HasNoLookAlikeFinding_AndDisplayEqualsCountingText()
    {
        var a = Analyze("Сэ Google-м сыкъэуцущт.");

        Assert.AreEqual(0, Count(a, FindingCodes.PalochkaLookAlike));
        Assert.AreEqual(a.Sentences[0].Text, a.Sentences[0].DisplayText);
    }

    // §4.5: a "|" standing in for the palochka does not split the word, and is shown as written.
    [TestMethod]
    public void PipeInsteadOfPalochka_IsOneWord_AndDisplayedAsPipe()
    {
        var a = Analyze("гущы|эр");

        Assert.AreEqual(1, a.Metrics.WordCount);
        Assert.AreEqual("гущы|эр", a.Sentences[0].DisplayText);
        Assert.AreEqual("гущыӏэр", a.Sentences[0].Text); // "|" is not upper case, so it becomes the lower-case palochka
    }

    // §4.6: an invisible character inside a word is reported, absent from the counting sentence,
    // present in the displayed one, and does not change the word count.
    [TestMethod]
    public void ZeroWidthJoinerInsideWord_IsShown_ButNotCounted()
    {
        var withJoiner = Analyze("сы‍щ дэгъу.");
        var without = Analyze("сыщ дэгъу.");

        Assert.AreEqual(1, Count(withJoiner, FindingCodes.Invisible));
        Assert.IsFalse(withJoiner.Sentences[0].Text.Contains('‍'));
        Assert.IsTrue(withJoiner.Sentences[0].DisplayText.Contains('‍'));
        Assert.AreEqual("сы‍щ дэгъу.", withJoiner.Sentences[0].DisplayText);
        Assert.AreEqual(without.Metrics.WordCount, withJoiner.Metrics.WordCount);
    }

    // §4.6, boundary cases: an invisible character at the start, end or next to a discarded list
    // marker is not lost, and the sentence boundaries do not move.
    [TestMethod]
    public void InvisibleCharacters_AtSentenceEdges_AreKept_AndBoundariesDoNotMove()
    {
        var a = Analyze("‍сэ дэгъу.‍ Тхьэ шӏу!\n\n- ‍мадде");

        Assert.AreEqual(3, a.Sentences.Count);
        var display = string.Concat(a.Sentences.Select(s => s.DisplayText));
        Assert.AreEqual(3, display.Count(c => c == '‍'));
        Assert.IsFalse(a.Sentences.Any(s => s.Text.Contains('‍')));
    }

    // §4.7: with no look-alikes and no invisible characters the two texts are identical.
    [TestMethod]
    public void WithoutLookAlikes_DisplayTextEqualsCountingText_CharacterForCharacter()
    {
        var a = Analyze("Уимафэ шӏу, о ухэт?\n\n1. Сэ унэфэщ.\n2. Ар «шъыпкъэ» ары!");

        Assert.IsTrue(a.Sentences.Count >= 3);
        foreach (var s in a.Sentences) Assert.AreEqual(s.Text, s.DisplayText);
    }

    // The same equality, over every real answer of the four tests whose raw files are in the repository.
    [TestMethod]
    public void RealData_DisplayTextDiffersFromCountingText_OnlyAtRepairedCharacters()
    {
        foreach (var (dir, test) in new[] { (TestPaths.RawRound1Dir, 1), (TestPaths.RawRound1Dir, 2) })
        {
            foreach (var r in ResultFileSetLoader.LoadTest(dir, test))
            {
                foreach (var q in r.Selected.Questions)
                {
                    var a = Analyze(q.AnswerText);
                    foreach (var s in a.Sentences)
                    {
                        Assert.AreEqual(s.Text.Length, s.DisplayText.Length, $"T{test}-A{r.Selected.Tool}-S{q.Number}");
                        for (var i = 0; i < s.Text.Length; i++)
                        {
                            if (s.Text[i] != s.DisplayText[i])
                            {
                                Assert.IsTrue(s.Text[i] is 'Ӏ' or 'ӏ', $"T{test}-A{r.Selected.Tool}-S{q.Number}: unexpected difference at {i}");
                            }
                        }
                    }
                }
            }
        }
    }

    // §4.8: the generated workbook's Sentences!E carries the displayed (original) text.
    [TestMethod]
    public void Workbook_SentencesColumnE_CarriesTheDisplayedText()
    {
        foreach (var test in new[] { 1, 2 })
        {
            var profile = TestProfiles.Ady;
            var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
            var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
            foreach (var r in resolved)
                foreach (var q in r.Selected.Questions)
                    analyzed[new ResponseId(test, r.Selected.Tool, q.Number)] = TextAnalyzer.Analyze(q.AnswerText, profile);

            var path = Path.Combine(Path.GetTempPath(), $"akt-rev14-{Guid.NewGuid():N}.xlsx");
            try
            {
                WorkbookBuilder.Build(path, new WorkbookInput(test, resolved, analyzed));

                using var document = SpreadsheetDocument.Open(path, false);
                var sst = document.WorkbookPart!.SharedStringTablePart;
                var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.SentencesSheetName)
                    .Where(r => r.RowIndex!.Value > 1).OrderBy(r => r.RowIndex!.Value).ToList();

                var expected = new List<string>();
                for (var question = 1; question <= 7; question++)
                    foreach (var toolFile in resolved.OrderBy(f => f.Selected.Tool))
                        expected.AddRange(analyzed[new ResponseId(test, toolFile.Selected.Tool, question)].Sentences.Select(s => s.DisplayText));

                Assert.AreEqual(expected.Count, rows.Count);
                for (var i = 0; i < rows.Count; i++)
                    Assert.AreEqual(expected[i], WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(rows[i], "E"), sst), $"test {test}, row {rows[i].RowIndex}");

                if (test == 1)
                {
                    var all = string.Concat(rows.Select(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "E"), sst)));
                    Assert.IsTrue(all.Contains("AI-компьютерым", StringComparison.Ordinal), "the original Latin AI must be shown");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
