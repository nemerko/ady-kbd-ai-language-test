using System.Text;

namespace AdyKbdTest.Instrument;

/// <summary>One row of <c>test{n}-notes.tsv</c> (revision 18 §3).</summary>
public sealed record NoteRow(string Sheet, string Key, int Tool, int Question, int? SentenceNo, string Note);

/// <summary>
/// Lossless one-line encoding of a free-text note for a TSV cell, and reading the file back.
/// A note is the evaluator's own text and must come out of the report byte for byte as it went in,
/// so tab and line breaks (which would split a row) are written as <c>\t</c>, <c>\n</c>, <c>\r</c>
/// and a literal backslash as <c>\\</c>.
/// </summary>
public static class NotesTsv
{
    public static string Escape(string note) =>
        note.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    public static string Unescape(string cell)
    {
        var sb = new StringBuilder(cell.Length);
        for (var i = 0; i < cell.Length; i++)
        {
            if (cell[i] == '\\' && i + 1 < cell.Length)
            {
                i++;
                sb.Append(cell[i] switch { 't' => '\t', 'r' => '\r', 'n' => '\n', var c => c });
            }
            else
            {
                sb.Append(cell[i]);
            }
        }
        return sb.ToString();
    }

    public static IReadOnlyList<NoteRow> Read(string path) =>
        File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'))
            .Select(f => new NoteRow(
                f[0], f[1],
                int.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture),
                f[4].Length == 0 ? null : int.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture),
                Unescape(f[5])))
            .ToList();
}
