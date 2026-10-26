using System.Text;
using AdyKbdTest.Instrument;

namespace AdyKbdTest.Tests;

/// <summary>
/// Revision 12 §1: <see cref="ResultFileParser.Parse"/> only ever parsed the first seven question
/// sections and stopped there unconditionally, so a file with an eighth section had it silently
/// dropped -- no exception, no warning, just a word/sentence count computed from six sevenths of
/// the file. The tool is specific to a seven-question instrument (§3 of the task document this
/// fixes explains why: Q4/Q5's scoring and Q5's copying probe and Q7's own verdict field do not
/// generalise to an arbitrary question count), so an eighth section is not new input to accept, it
/// is a file this tool cannot score correctly and must reject instead.
/// </summary>
[TestClass]
public sealed class ResultFileSectionTests
{
    // Matches the real template exactly (see e.g. data/raw/round1/Test1-AI-Tool1-Result.txt):
    // "CEVAP S{n}:" on its own line, nothing after the colon -- the answer text is the following
    // line(s), up to the closing "====" rule.
    private static string BuildFileContent(int questionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================================================");
        sb.AppendLine("Header");
        sb.AppendLine("========================================================================");
        sb.AppendLine();
        for (var n = 1; n <= questionCount; n++)
        {
            sb.AppendLine($"S{n}: Question {n} text");
            sb.AppendLine("------------------------------------------------------------------------");
            sb.AppendLine($"CEVAP S{n}:");
            sb.AppendLine($"Answer {n} text.");
            sb.AppendLine("========================================================================");
        }
        return sb.ToString();
    }

    private static string WriteTempFile(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"akt-rev12-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Test1-AI-Tool1-Result.txt");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    // Baseline: a well-formed seven-question file parses exactly as it always has.
    [TestMethod]
    public void Parse_SevenQuestionFile_ParsesAllSeven()
    {
        var path = WriteTempFile(BuildFileContent(7));
        try
        {
            var file = ResultFileParser.Parse(path);
            Assert.AreEqual(7, file.Questions.Count);
            Assert.IsTrue(file.AllAnswersNonEmpty);
            Assert.AreEqual("Answer 7 text.", file.Questions[6].AnswerText);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    // The bug this fixes: an eighth question section must now be rejected, not silently dropped.
    [TestMethod]
    public void Parse_EightQuestionFile_IsRejected()
    {
        var path = WriteTempFile(BuildFileContent(8));
        try
        {
            var ex = Assert.ThrowsException<ResultFileFormatException>(() => ResultFileParser.Parse(path));
            StringAssert.Contains(ex.Message, "seven-question instrument");
            StringAssert.Contains(ex.Message, "S8");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    // A short file must keep failing the way it always has -- "question S7 not found" -- not the
    // new check, which only looks for content *beyond* S7 and has nothing to say about a file that
    // never reaches S7 in the first place.
    [TestMethod]
    public void Parse_SixQuestionFile_StillFailsWithTheOriginalMessage()
    {
        var path = WriteTempFile(BuildFileContent(6));
        try
        {
            var ex = Assert.ThrowsException<ResultFileFormatException>(() => ResultFileParser.Parse(path));
            StringAssert.Contains(ex.Message, "S7");
            Assert.IsFalse(ex.Message.Contains("seven-question instrument", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
