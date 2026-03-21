using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }

    public Guid MenuItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    [MaxLength(500)]
    public string? Modifiers { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public Order? Order { get; set; }
    public MenuItem? MenuItem { get; set; }
}
