using System.Text.Json.Serialization;

namespace menu_backend.DTOs.Staff;

public class StaffImportRow : ImportRowBase
{
    public string FullName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public decimal? MonthlySalary { get; set; }

    /// <summary>True when an initial password was given (the password itself is never sent back).</summary>
    public bool HasPassword { get; set; }

    [JsonIgnore]
    public string? Password { get; set; }
}

public class StaffImportResult : ImportResult<StaffImportRow>
{
}
