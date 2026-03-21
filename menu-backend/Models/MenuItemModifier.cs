using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class MenuItemModifier : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal AdditionalPrice { get; set; } = 0;

    public bool IsAvailable { get; set; } = true;

    // Foreign Keys
    public Guid MenuItemId { get; set; }

    // Navigation
    public MenuItem? MenuItem { get; set; }
}
