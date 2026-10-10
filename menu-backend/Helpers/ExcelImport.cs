using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace menu_backend.Helpers;

/// <summary>A column in an import sheet: template header, accepted header aliases and parsing hints.</summary>
public record ExcelColumn(
    string Field,
    string Header,
    string[] Aliases,
    bool Required = false,
    double Width = 16,
    string? NumberFormat = null)
{
    /// <summary>Header name without the "*" required marker or bracketed hints, for messages.</summary>
    public string DisplayName => Regex.Replace(Header, @"\s*\*|\s*\(.*?\)", "").Trim();
}

/// <summary>
/// Shared Excel import plumbing: builds templates and reads uploaded sheets leniently
/// (header aliases, "₹ 1,250.50", "Yes/No/Y/N", blank rows, bracketed header hints).
/// </summary>
public static class ExcelImport
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Returns an error message for an unusable upload, or null if it's fine to parse.</summary>
    public static string? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0) return "Choose an Excel file to upload.";
        if (file.Length > MaxFileBytes) return "File is larger than 5 MB.";
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return "Upload an Excel .xlsx file. In Excel, use File → Save As → Excel Workbook (.xlsx).";
        return null;
    }

    /// <summary>Normalized comparison key: lowercase letters/digits only.</summary>
    public static string Key(string? s) =>
        new string((s ?? string.Empty).ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '₹').ToArray());

    // ── Template ──

    /// <summary>
    /// Data sheet with styled headers only (no sample rows — an untouched template must never
    /// overwrite real records), plus an Instructions sheet with notes and an example table.
    /// </summary>
    public static byte[] BuildTemplate(string sheetName, IReadOnlyList<ExcelColumn> columns, string title,
        IEnumerable<string> notes, IEnumerable<object[]> exampleRows)
    {
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add(sheetName);
        for (var i = 0; i < columns.Count; i++)
        {
            ws.Cell(1, i + 1).Value = columns[i].Header;
            ws.Column(i + 1).Width = columns[i].Width;
            if (columns[i].NumberFormat != null)
                ws.Column(i + 1).Style.NumberFormat.Format = columns[i].NumberFormat;
        }
        var header = ws.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E74C3C");
        header.Style.Font.FontColor = XLColor.White;
        ws.SheetView.FreezeRows(1);

        var help = wb.Worksheets.Add("Instructions");
        var lines = new List<string> { title, "" };
        lines.AddRange(notes);
        lines.Add("");
        lines.Add($"Example (fill the {sheetName} sheet like this):");
        for (var i = 0; i < lines.Count; i++)
            help.Cell(i + 1, 1).Value = lines[i];
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;

        var top = lines.Count + 1;
        for (var c = 0; c < columns.Count; c++)
            help.Cell(top, c + 1).Value = columns[c].Header;
        var r = top;
        foreach (var example in exampleRows)
        {
            r++;
            for (var c = 0; c < example.Length; c++)
                help.Cell(r, c + 1).Value = XLCellValue.FromObject(example[c]);
        }
        var exHeader = help.Range(top, 1, top, columns.Count);
        exHeader.Style.Font.Bold = true;
        exHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        help.Range(top, 1, r, columns.Count).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        for (var c = 0; c < columns.Count; c++)
            help.Column(c + 1).Width = columns[c].Width;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Reading ──

    /// <summary>
    /// Opens the sheet (the one named <paramref name="sheetName"/>, else the first), maps headers to
    /// fields and parses every non-blank data row with <paramref name="parse"/> (rows are only
    /// readable while the workbook is open). Throws ArgumentException with a user-facing message for
    /// unreadable files, missing required columns or too many rows.
    /// </summary>
    public static List<T> ReadRows<T>(Stream xlsx, string sheetName, IReadOnlyList<ExcelColumn> columns, int maxRows,
        Func<SheetRow, T> parse)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(xlsx); }
        catch (Exception)
        {
            throw new ArgumentException("Could not read the file. Upload an Excel .xlsx file (save as \"Excel Workbook\" if it's .xls or .csv).");
        }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(s => Key(s.Name) == Key(sheetName)) ?? wb.Worksheet(1);
            var used = ws.RangeUsed() ?? throw new ArgumentException("The sheet is empty.");

            var colIndex = new Dictionary<string, int>();
            foreach (var cell in used.FirstRow().Cells())
            {
                // Ignore hints in brackets: "Veg (Yes/No)" → "veg", "Prep Time (mins)" → "preptime"
                var h = Key(Regex.Replace(cell.GetString(), @"\(.*?\)|\[.*?\]", ""));
                foreach (var col in columns)
                    if (!colIndex.ContainsKey(col.Field) && col.Aliases.Contains(h))
                        colIndex[col.Field] = cell.Address.ColumnNumber;
            }

            var missing = columns.Where(c => c.Required && !colIndex.ContainsKey(c.Field)).Select(c => c.DisplayName).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Missing column(s): {string.Join(", ", missing)}. Download the template to see the expected headers.");

            var rows = new List<T>();
            foreach (var row in used.RowsUsed().Skip(1).Select(r => r.WorksheetRow()))
            {
                if (colIndex.Values.All(c => string.IsNullOrWhiteSpace(row.Cell(c).GetFormattedString())))
                    continue;
                if (rows.Count >= maxRows)
                    throw new ArgumentException($"The file has more than {maxRows} rows. Split it into smaller files.");
                rows.Add(parse(new SheetRow(row, colIndex)));
            }

            if (rows.Count == 0)
                throw new ArgumentException("No rows found below the header row.");
            return rows;
        }
    }

    /// <summary>Yes/No-style values. Returns null for unrecognized text; blank returns the default.</summary>
    public static bool? ParseYesNo(string value, bool defaultValue)
    {
        var v = Key(value);
        if (v.Length == 0) return defaultValue;
        return v switch
        {
            "yes" or "y" or "true" or "1" or "active" => true,
            "no" or "n" or "false" or "0" or "inactive" => false,
            _ => null
        };
    }
}

/// <summary>One data row of an uploaded sheet, read by field name.</summary>
public class SheetRow
{
    private readonly IXLRow _row;
    private readonly Dictionary<string, int> _cols;

    public SheetRow(IXLRow row, Dictionary<string, int> cols)
    {
        _row = row;
        _cols = cols;
    }

    public int RowNumber => _row.RowNumber();

    public bool HasColumn(string field) => _cols.ContainsKey(field);

    /// <summary>Trimmed display text of the cell ("" if the column is absent or blank).</summary>
    public string Text(string field) =>
        _cols.TryGetValue(field, out var c) ? _row.Cell(c).GetFormattedString().Trim() : string.Empty;

    public string? TextOrNull(string field)
    {
        var t = Text(field);
        return t.Length == 0 ? null : t;
    }

    /// <summary>True when the cell has a value (used to decide what an update may overwrite).</summary>
    public bool Has(string field) => Text(field).Length > 0;

    /// <summary>
    /// Parses a number from a numeric cell or text like "₹ 1,250.50" / "12 kg". Returns null when
    /// blank; adds an error to <paramref name="errors"/> when present but not a number.
    /// </summary>
    public decimal? Decimal(string field, string label, List<string> errors)
    {
        if (!_cols.TryGetValue(field, out var c)) return null;
        var cell = _row.Cell(c);
        if (cell.DataType == XLDataType.Number) return Math.Round((decimal)cell.GetDouble(), 4);

        var raw = cell.GetString().Trim();
        if (raw.Length == 0) return null;
        var cleaned = Regex.Replace(raw, @"(?i)₹|rs\.?|inr|,", "").Trim();
        var m = Regex.Match(cleaned, @"^-?\d+(\.\d+)?");
        if (m.Success && decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;
        errors.Add($"{label} \"{raw}\" is not a number.");
        return null;
    }
}
