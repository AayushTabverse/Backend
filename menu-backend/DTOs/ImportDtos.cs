using System.Text.Json.Serialization;

namespace menu_backend.DTOs;

/// <summary>Shared shape of one parsed Excel row in any import (menu, staff, inventory).</summary>
public abstract class ImportRowBase
{
    /// <summary>Row number in the Excel sheet (header is row 1).</summary>
    public int RowNumber { get; set; }

    /// <summary>Create, Update, Unchanged (exists, nothing differs), Skip (exists, updates off) or Error.</summary>
    public string Action { get; set; } = "Create";

    public List<string> Errors { get; set; } = new();

    /// <summary>For Update rows: what will change, e.g. "Price ₹240 → ₹260".</summary>
    public List<string> Changes { get; set; } = new();

    /// <summary>
    /// Fields that had a value in the sheet. On update only these are written, so a blank cell
    /// or a missing column never wipes existing data.
    /// </summary>
    [JsonIgnore]
    public HashSet<string> Provided { get; } = new();
}

public class ImportResult<TRow> where TRow : ImportRowBase
{
    public List<TRow> Rows { get; set; } = new();

    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public int UnchangedCount { get; set; }
    public int SkipCount { get; set; }
    public int ErrorCount { get; set; }

    /// <summary>False for a preview; true once changes were saved.</summary>
    public bool Applied { get; set; }

    public void Tally()
    {
        CreateCount = Rows.Count(r => r.Action == "Create");
        UpdateCount = Rows.Count(r => r.Action == "Update");
        UnchangedCount = Rows.Count(r => r.Action == "Unchanged");
        SkipCount = Rows.Count(r => r.Action == "Skip");
        ErrorCount = Rows.Count(r => r.Action == "Error");
    }
}
