using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AdyKbdTest.Instrument;

/// <summary>Cell-reading helpers shared by <see cref="WorkbookReader"/> and <see cref="WorkbookBuilder"/>'s overwrite-protection check, so both read a saved workbook's cells the same way (inline strings as generated, shared strings as Excel commonly rewrites them to on save). Public because tests need it too, to inspect a generated workbook's raw structure.</summary>
public static class WorkbookXmlHelpers
{
    public static Cell? GetCell(Row row, string column) =>
        row.Elements<Cell>().FirstOrDefault(c => ColumnOf(c.CellReference?.Value) == column);

    public static string ColumnOf(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference)) return "";
        var i = 0;
        while (i < cellReference.Length && char.IsLetter(cellReference[i])) i++;
        return cellReference[..i];
    }

    /// <summary>1-based column index: A=1, B=2, ..., Z=26, AA=27, ...</summary>
    public static int ColumnIndex(string column)
    {
        var index = 0;
        foreach (var c in column) index = index * 26 + (c - 'A' + 1);
        return index;
    }

    public static int ColumnIndexOf(string? cellReference) => ColumnIndex(ColumnOf(cellReference));

    public static string? Text(Cell? cell, SharedStringTablePart? sstPart)
    {
        if (cell is null) return null;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (sstPart is null || cell.CellValue is null) return null;
            if (!int.TryParse(cell.CellValue.Text, out var idx)) return null;
            var items = sstPart.SharedStringTable.Elements<SharedStringItem>().ToList();
            return idx >= 0 && idx < items.Count ? items[idx].InnerText : null;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text ?? cell.InlineString?.InnerText;
        }

        return cell.CellValue?.Text;
    }

    public static IEnumerable<Row> ReadRows(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault(s => s.Name == sheetName)
            ?? throw new InvalidOperationException($"Sheet \"{sheetName}\" not found in workbook.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        return worksheetPart.Worksheet.Descendants<SheetData>().First()
            .Elements<Row>()
            .OrderBy(r => r.RowIndex?.Value ?? 0);
    }
}
