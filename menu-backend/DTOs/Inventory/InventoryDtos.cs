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

// ═══ Inventory analytics: stock health now + consumption over a period ═══

public class InventoryAnalyticsResponse
{
    public int Days { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    // Snapshot (now)
    public int TotalItems { get; set; }
    public int InStock { get; set; }
    public int LowStock { get; set; }
    public int OutOfStock { get; set; }
    public decimal StockValue { get; set; }
    public int ItemsWithoutCost { get; set; }
    public int ItemsWithoutMinimum { get; set; }

    // Period (from the activity log), valued at current cost per unit
    public menu_backend.DTOs.Analytics.Metric UsedValue { get; set; } = new();
    public menu_backend.DTOs.Analytics.Metric WastedValue { get; set; } = new();
    public menu_backend.DTOs.Analytics.Metric RestockedValue { get; set; } = new();
    /// <summary>Wasted as % of (used + wasted).</summary>
    public menu_backend.DTOs.Analytics.Metric WasteRate { get; set; } = new();
    public int AutoDeductions { get; set; }

    public List<InventoryDailyPoint> Daily { get; set; } = new();
    public List<StockRunway> RunningOut { get; set; } = new();
    public List<InventoryItemMovement> TopUsed { get; set; } = new();
    public List<InventoryItemMovement> TopWasted { get; set; } = new();
    public List<InventoryItemMovement> TopValue { get; set; } = new();
    public List<InventoryItemMovement> DeadStock { get; set; } = new();
    public decimal DeadStockValue { get; set; }
    public List<CategorySummary> ValueByCategory { get; set; } = new();
    public List<menu_backend.DTOs.Analytics.Insight> Insights { get; set; } = new();
}

public class InventoryDailyPoint
{
    public DateOnly Date { get; set; }
    public decimal UsedValue { get; set; }
    public decimal WastedValue { get; set; }
    public decimal RestockedValue { get; set; }
}

public class StockRunway
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public decimal AvgDailyUse { get; set; }
    /// <summary>Days until empty at the period's average daily use.</summary>
    public decimal DaysLeft { get; set; }
    /// <summary>Quantity to order for two weeks of cover (items listed run out within 7 days).</summary>
    public decimal SuggestedOrder { get; set; }
    public string? Supplier { get; set; }
}

public class InventoryItemMovement
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    public decimal SharePct { get; set; }
}
