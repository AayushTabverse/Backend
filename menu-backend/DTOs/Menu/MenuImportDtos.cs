namespace menu_backend.DTOs.Menu;

public class MenuImportRow : ImportRowBase
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public bool IsVeg { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int PreparationTimeMinutes { get; set; } = 15;
    public string? ImageUrl { get; set; }
}

public class MenuImportResult : ImportResult<MenuImportRow>
{
    /// <summary>Categories in the file that don't exist yet and will be created.</summary>
    public List<string> NewCategories { get; set; } = new();
}
