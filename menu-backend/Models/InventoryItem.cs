using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum InventoryUnit
{
    Kg,
    Gram,
    Liter,
    Ml,
    Piece,
    Dozen,
    Box,
    Packet,
    Bottle,
    Can,
    Bunch
}

public class InventoryItem : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public decimal CurrentQuantity { get; set; } = 0;

    public InventoryUnit Unit { get; set; } = InventoryUnit.Piece;

    /// <summary>
    /// When quantity falls below this, trigger a reminder/alert.
    /// </summary>
    public decimal MinimumQuantity { get; set; } = 0;

    /// <summary>
    /// Cost per unit (for analytics).
    /// </summary>
    public decimal CostPerUnit { get; set; } = 0;

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(200)]
    public string? SupplierContact { get; set; }

    public DateTime? LastRestockedAt { get; set; }

    public bool IsActive { get; set; } = true;
}

public class InventoryLog : BaseEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// Positive = restock/addition, Negative = usage/wastage
    /// </summary>
    public decimal QuantityChange { get; set; }

    public decimal QuantityAfter { get; set; }

    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = "Restock"; // Restock, Usage, Wastage, Adjustment

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    // Navigation
    public InventoryItem? InventoryItem { get; set; }
}
