using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Inventory;

public class CreateInventoryItemRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public decimal CurrentQuantity { get; set; } = 0;

    [Required]
    public string Unit { get; set; } = "Piece";

    public decimal MinimumQuantity { get; set; } = 0;

    public decimal CostPerUnit { get; set; } = 0;

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(200)]
    public string? SupplierContact { get; set; }
}

public class UpdateInventoryItemRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public string? Unit { get; set; }

    public decimal? MinimumQuantity { get; set; }

    public decimal? CostPerUnit { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(200)]
    public string? SupplierContact { get; set; }

    public bool? IsActive { get; set; }
}

public class AdjustQuantityRequest
{
    [Required]
    public decimal Quantity { get; set; }

    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = "Restock"; // Restock, Usage, Wastage, Adjustment

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class InventoryItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal CurrentQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal MinimumQuantity { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierContact { get; set; }
    public DateTime? LastRestockedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InventoryLogResponse
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal QuantityAfter { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InventorySummaryResponse
{
    public int TotalItems { get; set; }
    public int LowStockItems { get; set; }
    public int OutOfStockItems { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public List<InventoryItemResponse> LowStockAlerts { get; set; } = new();
    public List<CategorySummary> CategoryBreakdown { get; set; } = new();
}

public class CategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalValue { get; set; }
}
