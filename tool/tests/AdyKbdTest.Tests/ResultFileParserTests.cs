using AdyKbdTest.Instrument;

namespace AdyKbdTest.Tests;

[TestClass]
public sealed class ResultFileParserTests
{
    [TestMethod]
    public void Parse_RealTool1File_ProducesSevenQuestionsWithNonEmptyAnswers()
    {
        var path = Path.Combine(TestPaths.RawRound1Dir, "Test1-AI-Tool1-Result.txt");
        var file = ResultFileParser.Parse(path);

        Assert.AreEqual(1, file.Test);
        Assert.AreEqual(1, file.Tool);
        Assert.AreEqual(1, file.Attempt);
        Assert.AreEqual(7, file.Questions.Count);
        Assert.IsTrue(file.AllAnswersNonEmpty);
        Assert.IsTrue(file.Questions[0].AnswerText.Length > 0);
        Assert.IsTrue(file.Questions[3].TurkishTranslation is not null); // S4 carries a Turkish gloss in parentheses
    }

    [TestMethod]
    public void Parse_RecorderNoteOnTheCevapLine_IsSeparatedFromTheAnswer()
    {
        var path = Path.Combine(TestPaths.RawRound1Dir, "Test2-AI-Tool5-Result.txt");
        var file = ResultFileParser.Parse(path);

        var q5 = file.Questions.Single(q => q.Number == 5);
        Assert.IsNotNull(q5.RecorderNote);
        Assert.IsTrue(q5.RecorderNote!.Contains("Grok", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_FileNameAttemptNumbering_IsRead()
    {
        var (test, tool, attempt1) = ResultFileParser.ParseFileName("Test2-AI-Tool5-Result.txt");
        var (_, _, attempt2) = ResultFileParser.ParseFileName("Test2-AI-Tool5-Result2.txt");

        Assert.AreEqual(2, test);
        Assert.AreEqual(5, tool);
        Assert.AreEqual(1, attempt1);
        Assert.AreEqual(2, attempt2);
    }

    [TestMethod]
    public void LoadTest_Test2Tool5_PicksTheCompleteRetryAttempt()
    {
        var resolved = ResultFileSetLoader.LoadTest(TestPaths.RawRound1Dir, 2);
        var tool5 = resolved.Single(r => r.Selected.Tool == 5);

        Assert.AreEqual(2, tool5.Selected.Attempt);
        Assert.IsTrue(tool5.Selected.AllAnswersNonEmpty);
        Assert.AreEqual(2, tool5.AllAttempts.Count);
    }

    [TestMethod]
    public void ResponseId_RoundTripsThroughToStringAndParse()
    {
        var id = new ResponseId(2, 3, 5);
        Assert.AreEqual("T2-A3-S5", id.ToString());
        Assert.AreEqual(id, ResponseId.Parse("T2-A3-S5"));
    }
}
