namespace menu_backend.DTOs.Inventory;

public class InventoryImportRow : ImportRowBase
{
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? CurrentQuantity { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public decimal? CostPerUnit { get; set; }
    public string? Category { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierContact { get; set; }
    public string? Description { get; set; }
}

public class InventoryImportResult : ImportResult<InventoryImportRow>
{
}
