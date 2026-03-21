using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class RestaurantTable : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string TableNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Label { get; set; }

    public int Capacity { get; set; } = 4;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? QrCodeUrl { get; set; }

    /// <summary>
    /// QR format: https://yourapp.com/menu/{tenantId}/{tableId}
    /// </summary>
    [MaxLength(500)]
    public string? QrData { get; set; }

    /// <summary>
    /// Set to true when a customer calls the waiter from their QR menu.
    /// </summary>
    public bool IsCallingWaiter { get; set; } = false;
    public DateTime? WaiterCalledAt { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
