using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class MenuItem : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsVeg { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// AR model URL (GLB/USDZ format) for AR menu feature.
    /// </summary>
    [MaxLength(500)]
    public string? ARModelUrl { get; set; }

    public int PreparationTimeMinutes { get; set; } = 15;

    // Foreign Keys
    public Guid CategoryId { get; set; }

    // Navigation
    public MenuCategory? Category { get; set; }
    public ICollection<MenuItemModifier> Modifiers { get; set; } = new List<MenuItemModifier>();
}
