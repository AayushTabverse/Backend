using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

/// <summary>
/// Links a MenuItem to an InventoryItem — specifies how much of an ingredient is used per unit of the menu item.
/// </summary>
public class MenuItemIngredient : BaseEntity
{
    [Required]
    public Guid MenuItemId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// How much of the inventory item is consumed per 1 unit of the menu item.
    /// e.g., 0.2 kg of tomatoes per burger.
    /// </summary>
    public decimal QuantityUsed { get; set; }

    // Navigation
    public MenuItem? MenuItem { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
