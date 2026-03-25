using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

public class Tenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(36)]
    [Column("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? InstagramUrl { get; set; }

    [MaxLength(500)]
    public string? FacebookUrl { get; set; }

    [MaxLength(500)]
    public string? TwitterUrl { get; set; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; set; }

    [MaxLength(500)]
    public string? GoogleMapsUrl { get; set; }

    [MaxLength(1000)]
    public string? UpiQrCodeUrl { get; set; }

    [MaxLength(20)]
    public string? PrinterWidth { get; set; } = "standard";

    public bool DirectPrint { get; set; } = false;

    public decimal CgstPercent { get; set; } = 2.5m;

    public decimal SgstPercent { get; set; } = 2.5m;

    public decimal ServiceChargePercent { get; set; } = 0m;


    [MaxLength(100)]
    public string? Subdomain { get; set; }

    [MaxLength(50)]
    public string? CurrencyCode { get; set; } = "INR";

    [MaxLength(50)]
    public string? TimeZone { get; set; } = "Asia/Kolkata";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
    public ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
}
