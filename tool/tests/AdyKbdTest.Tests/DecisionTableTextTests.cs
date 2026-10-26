using AdyKbdTest.Instrument;
using AdyKbdTest.TextAnalysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 10: the decision table's Code column matches the code dictionary's "CODE: root / root
/// / root" cells exactly, from one shared source (§1); all five questions were rewritten in three
/// languages, with INV's unverifiable second sentence removed (§2); and two boundary paragraphs
/// were added below the table's existing "one code per word" rule (§3). No code, category,
/// category boundary, table order, formula, cell type or column changed.
/// </summary>
[TestClass]
public sealed class DecisionTableTextTests
{
    private static WorkbookInput BuildRealInput(int test)
    {
        var profile = TestProfiles.Ady;
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, test);
        var analyzed = new Dictionary<ResponseId, AnalyzedResponse>();
        foreach (var r in resolved)
        {
            foreach (var q in r.Selected.Questions)
            {
                analyzed[new ResponseId(test, r.Selected.Tool, q.Number)] = TextAnalyzer.Analyze(q.AnswerText, profile);
            }
        }
        return new WorkbookInput(test, resolved, analyzed);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"akt-rev10-test-{Guid.NewGuid():N}.xlsx");

    private static (List<Row> Rows, DocumentFormat.OpenXml.Packaging.SharedStringTablePart? Sst) ReadInfoSheet(SpreadsheetDocument document)
    {
        var sstPart = document.WorkbookPart!.SharedStringTablePart;
        var rows = WorkbookXmlHelpers.ReadRows(document.WorkbookPart!, WorkbookBuilder.InfoSheetName).OrderBy(r => r.RowIndex!.Value).ToList();
        return (rows, sstPart);
    }

    // §1: the decision table's Code column is byte-identical to the code dictionary's, because both
    // are read from the same source (WordCodeRootExpansions) -- not two hand-typed copies.
    [TestMethod]
    public void DecisionTable_CodeColumn_MatchesCodeDictionary_ForEveryCode()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            using var document = SpreadsheetDocument.Open(path, false);
            var (rows, sst) = ReadInfoSheet(document);
            string? A(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sst);
            string? B(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst);

            var decisionStart = rows.FindIndex(r => B(r) == "Decision table");
            var dictionaryStart = rows.FindIndex(decisionStart + 1, r => B(r) == "Code dictionary");
            Assert.IsTrue(decisionStart >= 0 && dictionaryStart > decisionStart);

            var decisionCodes = rows.Skip(decisionStart + 1).Take(dictionaryStart - decisionStart - 1)
                .Select(A).Where(t => t is not null && t != "Code").ToList();
            Assert.AreEqual(5, decisionCodes.Count);

            foreach (var code in decisionCodes)
            {
                // Every decision-table code cell must also appear, unchanged, somewhere in the
                // code dictionary that follows it.
                Assert.IsTrue(rows.Skip(dictionaryStart).Any(r => A(r) == code), $"\"{code}\" not found in the code dictionary");
                // Revision 10 §1: the two English-only parentheticals ("MIX (+ contact language)",
                // "TRM (not an error)") are gone -- their meaning moved into the question text.
                Assert.IsFalse(code!.Contains('(', StringComparison.Ordinal), $"\"{code}\" still has a parenthetical");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §2: INV's second sentence ("does such a word not exist in any language?") is gone, in all
    // three languages -- it asked evaluators to verify something unverifiable, and MIX already
    // being checked first (revision 6 §4) makes it redundant besides.
    [TestMethod]
    public void DecisionTable_InvQuestion_HasNoUnverifiableSecondSentence()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            using var document = SpreadsheetDocument.Open(path, false);
            var (rows, sst) = ReadInfoSheet(document);
            string? B(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst);
            var decisionStart = rows.FindIndex(r => B(r) == "Decision table");
            var dictionaryStart = rows.FindIndex(decisionStart + 1, r => B(r) == "Code dictionary");
            var decisionRows = rows.Skip(decisionStart + 1).Take(dictionaryStart - decisionStart - 1);
            var invRow = decisionRows.Single(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sst) == "INV: invented / uydurma / выдуманное");

            var en = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(invRow, "B"), sst)!;
            var tr = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(invRow, "C"), sst)!;
            var ru = WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(invRow, "D"), sst)!;

            Assert.IsFalse(en.Contains("any language", StringComparison.Ordinal), en);
            Assert.IsFalse(tr.Contains("hiçbir dilde", StringComparison.Ordinal), tr);
            Assert.IsFalse(ru.Contains("ни в одном языке", StringComparison.Ordinal), ru);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §2: the exact question text for all five rows, verbatim, in all three languages -- the task
    // document was explicit that none of this may be reworded, "corrected", or re-translated.
    [TestMethod]
    public void DecisionTable_QuestionText_IsExactlyAsGiven()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            using var document = SpreadsheetDocument.Open(path, false);
            var (rows, sst) = ReadInfoSheet(document);
            string? B(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst);
            var decisionStart = rows.FindIndex(r => B(r) == "Decision table");
            var dictionaryStart = rows.FindIndex(decisionStart + 1, r => B(r) == "Code dictionary");
            var decisionRows = rows.Skip(decisionStart + 1).Take(dictionaryStart - decisionStart - 1).ToList();

            (string En, string Tr, string Ru) Question(string code)
            {
                var row = decisionRows.Single(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "A"), sst) == code);
                return (
                    WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "B"), sst)!,
                    WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "C"), sst)!,
                    WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(row, "D"), sst)!);
            }

            var frm = Question("FRM: form / biçim / форма");
            Assert.AreEqual("Is the right word chosen, but its affix or inflection wrong?", frm.En);
            Assert.AreEqual("Doğru sözcük seçilmiş ama eki ya da çekimi mi yanlış?", frm.Tr);
            Assert.AreEqual("Слово выбрано правильно, но неверен его аффикс или словоизменение?", frm.Ru);

            var cho = Question("CHO: choice / seçim / выбор");
            Assert.AreEqual("Is the word well formed, but the wrong word for the intended meaning?", cho.En);
            Assert.AreEqual("Sözcük doğru kurulmuş ama anlam için yanlış sözcük mü seçilmiş?", cho.Tr);
            Assert.AreEqual("Слово образовано правильно, но по смыслу выбрано не то слово?", cho.Ru);

            var mix = Question("MIX: mixing / karışma / смешение");
            Assert.AreEqual("Is the word taken from another language and left in that language's own form? The contact language is recorded as well.", mix.En);
            Assert.AreEqual("Sözcük başka bir dilden, o dilin kendi biçimiyle olduğu gibi mi alınmış? Kaynak dil ayrıca kaydedilir.", mix.Tr);
            Assert.AreEqual("Слово взято из другого языка и оставлено в его собственной форме? Язык-источник записывается отдельно.", mix.Ru);

            var inv = Question("INV: invented / uydurma / выдуманное");
            Assert.AreEqual("Even if the root and affixes are familiar, is this a combination the language does not use?", inv.En);
            Assert.AreEqual("Kök ve ekler tanıdık olsa bile, bu birleşimi dil kullanmıyor mu?", inv.Tr);
            Assert.AreEqual("Даже если корень и аффиксы знакомы, такое сочетание в языке не используется?", inv.Ru);

            var trm = Question("TRM: term / terim / термин");
            Assert.AreEqual("Is the word an established loan, adapted to the language, used where a recognised native term exists? This is not an error.", trm.En);
            Assert.AreEqual("Sözcük, dile uyarlanmış yerleşik bir alıntı ama kabul görmüş yerli bir karşılığı var mı? Hata değildir.", trm.Tr);
            Assert.AreEqual("Слово — устоявшееся заимствование, приспособленное к языку, но есть признанный местный термин? Это не ошибка.", trm.Ru);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §3: the two new boundary paragraphs are present, in all three languages, after the existing
    // "one code per word" paragraph and before the code dictionary.
    [TestMethod]
    public void DecisionTable_HasTwoNewBoundaryParagraphs()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            using var document = SpreadsheetDocument.Open(path, false);
            var (rows, sst) = ReadInfoSheet(document);
            string? B(Row r) => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst);

            var decisionStart = rows.FindIndex(r => B(r) == "Decision table");
            var dictionaryStart = rows.FindIndex(decisionStart + 1, r => B(r) == "Code dictionary");
            var sectionText = string.Join("\n", rows.Skip(decisionStart).Take(dictionaryStart - decisionStart)
                .SelectMany(r => r.Elements<Cell>())
                .Select(c => WorkbookXmlHelpers.Text(c, sst))
                .Where(t => t is not null));

            // The existing rule paragraph (unchanged, per §2.2) stays put ahead of the two new ones.
            StringAssert.Contains(sectionText, "Each word is counted under exactly one code");

            StringAssert.Contains(sectionText, "A practical way to tell these three apart");
            StringAssert.Contains(sectionText, "Bu üçünü ayırmanın pratik yolu");
            StringAssert.Contains(sectionText, "Практический способ различить эти три");

            StringAssert.Contains(sectionText, "The boundary between MIX and TRM is adaptation");
            StringAssert.Contains(sectionText, "MIX ile TRM arasındaki ayrım uyarlamadır");
            StringAssert.Contains(sectionText, "Граница между MIX и TRM");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // §5 acceptance: row heights for the decision table's five question rows are computed, not left
    // at the sheet default -- the questions are full sentences now, long enough to wrap.
    [TestMethod]
    public void DecisionTable_QuestionRows_HaveComputedHeight()
    {
        var path = TempPath();
        try
        {
            WorkbookBuilder.Build(path, BuildRealInput(1));
            using var document = SpreadsheetDocument.Open(path, false);
            var workbookPart = document.WorkbookPart!;
            var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == WorkbookBuilder.InfoSheetName);
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            var sst = workbookPart.SharedStringTablePart;

            var rows = worksheetPart.Worksheet.Descendants<SheetData>().First().Elements<Row>().OrderBy(r => r.RowIndex!.Value).ToList();
            var decisionStart = rows.FindIndex(r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst) == "Decision table");
            var dictionaryStart = rows.FindIndex(decisionStart + 1, r => WorkbookXmlHelpers.Text(WorkbookXmlHelpers.GetCell(r, "B"), sst) == "Code dictionary");
            var questionRows = rows.Skip(decisionStart + 2).Take(dictionaryStart - decisionStart - 2); // skip section header + "Code/Question/Soru/Вопрос" row

            foreach (var row in questionRows)
            {
                Assert.IsTrue(row.CustomHeight?.Value ?? false, $"row {row.RowIndex} has no custom height");
                Assert.IsTrue((row.Height?.Value ?? 0) > 15, $"row {row.RowIndex} height was not computed above the single-line default");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
