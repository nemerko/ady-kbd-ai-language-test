using System.Text.RegularExpressions;

namespace AdyKbdTest.Instrument;

/// <summary>
/// Parses one "Test{n}-AI-Tool{m}-Result[k].txt" session-log file into a <see cref="RawResultFile"/>.
/// The template uses two kinds of rule line as structural markers: a line of only "=" separates
/// whole sections (banner, each Q/A block); a line of only "-" is the sub-rule that sits right
/// before a "CEVAP S{n}:" line. Everything else is read relative to those.
/// </summary>
public static partial class ResultFileParser
{
    [GeneratedRegex(@"^Test(?<test>\d+)-AI-Tool(?<tool>\d+)-Result(?<attempt>\d*)\.txt$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();

    [GeneratedRegex(@"^=+$")]
    private static partial Regex EqualsSeparatorRegex();

    [GeneratedRegex(@"^-+$")]
    private static partial Regex DashSeparatorRegex();

    [GeneratedRegex(@"^ {2,}(?<label>\S(?:.*?\S)?) {2,}:[ \t]?(?<value>.*)$")]
    private static partial Regex HeaderFieldRegex();

    [GeneratedRegex(@"^(?<q>.*?)\s*\((?<tr>[^()]*)\)\s*$")]
    private static partial Regex TrailingParenRegex();

    [GeneratedRegex(@"^CEVAP S(?<n>\d+):")]
    private static partial Regex CevapLineRegex();

    public static (int Test, int Tool, int Attempt) ParseFileName(string fileName)
    {
        var m = FileNameRegex().Match(fileName);
        if (!m.Success)
        {
            throw new ResultFileFormatException(fileName,
                "file name does not match \"Test{n}-AI-Tool{m}-Result[k].txt\".");
        }

        var test = int.Parse(m.Groups["test"].Value);
        var tool = int.Parse(m.Groups["tool"].Value);
        var attemptText = m.Groups["attempt"].Value;
        var attempt = attemptText.Length == 0 ? 1 : int.Parse(attemptText);
        return (test, tool, attempt);
    }

    public static RawResultFile Parse(string path)
    {
        var fileName = Path.GetFileName(path);
        var (test, tool, attempt) = ParseFileName(fileName);

        var raw = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = raw.Split('\n');

        var firstQuestionLine = FindFirstQuestionLine(lines);
        if (firstQuestionLine < 0)
        {
            throw new ResultFileFormatException(path, "no \"S1:\" (or any \"S{n}:\") line found; not a recognisable session log.");
        }

        var headerFields = ParseHeaderFields(lines, 0, firstQuestionLine);

        var questions = new List<RawQuestion>(7);
        var cursor = 0;
        for (var n = 1; n <= 7; n++)
        {
            var (question, nextCursor) = ParseQuestion(path, lines, cursor, n);
            questions.Add(question);
            cursor = nextCursor;
        }

        // Revision 12 §1: the loop above stops at S7 unconditionally, so a file with an eighth
        // question section previously had it silently dropped -- no exception, no warning, just a
        // word/sentence count computed from six sevenths of the file. This tool is built for a
        // seven-question instrument specifically (§3 of the task document this fixes: Q4/Q5 are
        // scored against pre-written reference translations, Q5 carries the copying probe, Q7 has
        // its own verdict field -- none of that generalises to an arbitrary question count), so an
        // eighth section is not new input to accept, it is a file this tool cannot correctly score.
        for (var i = cursor; i < lines.Length; i++)
        {
            var extra = CevapLineRegex().Match(lines[i]);
            if (extra.Success && int.Parse(extra.Groups["n"].Value) > 7)
            {
                throw new ResultFileFormatException(path,
                    "this tool is built for a seven-question instrument, but the file has an extra "
                        + $"question section beyond S7 (\"{lines[i].Trim()}\"); file rejected.");
            }
        }

        return new RawResultFile
        {
            Test = test,
            Tool = tool,
            Attempt = attempt,
            SourceFileName = fileName,
            SourcePath = path,
            HeaderFields = headerFields,
            Questions = questions,
        };
    }

    private static int FindFirstQuestionLine(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^S1:\s?")) return i;
        }
        return -1;
    }

    private static Dictionary<string, string> ParseHeaderFields(string[] lines, int start, int endExclusive)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? lastLabel = null;

        for (var i = start; i < endExclusive; i++)
        {
            var line = lines[i];
            var m = HeaderFieldRegex().Match(line);
            if (m.Success)
            {
                var label = m.Groups["label"].Value.Trim();
                var value = m.Groups["value"].Value.Trim();
                fields[label] = value;
                lastLabel = label;
                continue;
            }

            var leadingSpaces = line.Length - line.TrimStart(' ').Length;
            if (lastLabel is not null && leadingSpaces >= 20 && line.Trim().Length > 0)
            {
                fields[lastLabel] = (fields[lastLabel] + " " + line.Trim()).Trim();
            }
        }

        return fields;
    }

    private static (RawQuestion Question, int NextCursor) ParseQuestion(string path, string[] lines, int searchFrom, int n)
    {
        var qLineRegex = new Regex($@"^S{n}:\s?(.*)$");

        var qLineIdx = -1;
        for (var i = searchFrom; i < lines.Length; i++)
        {
            if (qLineRegex.IsMatch(lines[i])) { qLineIdx = i; break; }
        }
        if (qLineIdx < 0)
        {
            throw new ResultFileFormatException(path, $"question S{n} not found.");
        }

        var firstLineRemainder = qLineRegex.Match(lines[qLineIdx]).Groups[1].Value;

        var dashIdx = -1;
        for (var i = qLineIdx + 1; i < lines.Length; i++)
        {
            if (DashSeparatorRegex().IsMatch(lines[i].Trim())) { dashIdx = i; break; }
        }
        if (dashIdx < 0)
        {
            throw new ResultFileFormatException(path, $"no rule line before the \"CEVAP S{n}:\" line was found after question S{n}.");
        }

        var questionLines = new List<string> { firstLineRemainder };
        for (var i = qLineIdx + 1; i < dashIdx; i++) questionLines.Add(lines[i]);
        var questionText = string.Join('\n', questionLines).Trim();

        string? turkishTranslation = null;
        var trMatch = TrailingParenRegex().Match(firstLineRemainder);
        if (trMatch.Success && trMatch.Groups["tr"].Value.Trim().Length > 0)
        {
            turkishTranslation = trMatch.Groups["tr"].Value.Trim();
        }

        var cevapRegex = new Regex($@"^CEVAP S{n}:(.*)$");
        var cevapIdx = -1;
        string recorderNoteRaw = "";
        for (var i = dashIdx + 1; i < lines.Length; i++)
        {
            var m = cevapRegex.Match(lines[i]);
            if (m.Success) { cevapIdx = i; recorderNoteRaw = m.Groups[1].Value; break; }
        }
        if (cevapIdx < 0)
        {
            throw new ResultFileFormatException(path, $"no \"CEVAP S{n}:\" line found for question S{n}.");
        }

        var endIdx = -1;
        for (var i = cevapIdx + 1; i < lines.Length; i++)
        {
            if (EqualsSeparatorRegex().IsMatch(lines[i].Trim())) { endIdx = i; break; }
        }
        if (endIdx < 0)
        {
            throw new ResultFileFormatException(path, $"no closing \"====\" rule found after \"CEVAP S{n}:\".");
        }

        var answerLines = lines[(cevapIdx + 1)..endIdx];
        var answerText = string.Join('\n', answerLines).Trim('\n', ' ', '\t').Trim();

        var recorderNote = recorderNoteRaw.Trim();
        var question = new RawQuestion
        {
            Number = n,
            QuestionText = questionText,
            TurkishTranslation = turkishTranslation,
            AnswerText = answerText,
            RecorderNote = recorderNote.Length > 0 ? recorderNote : null,
        };

        return (question, endIdx + 1);
    }
}
