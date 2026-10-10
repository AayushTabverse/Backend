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

    public decimal MaxDiscountPercent { get; set; } = 0m;

    public bool SpinWheelEnabled { get; set; } = false;


    [MaxLength(100)]
    public string? Subdomain { get; set; }

    /// <summary>
    /// Outcome of the last DNS record attempt for <see cref="Subdomain"/>:
    /// "created", "failed" or "not_configured" (no DNS API token on the server). Null = never attempted.
    /// </summary>
    [MaxLength(20)]
    public string? SubdomainDnsStatus { get; set; }

    /// <summary>Owner-safe reason when the DNS step failed.</summary>
    [MaxLength(500)]
    public string? SubdomainDnsError { get; set; }

    public DateTime? SubdomainDnsUpdatedAt { get; set; }

    // ── Bring-your-own domain (e.g. www.spicegarden.in) ──

    /// <summary>Restaurant's own domain, lowercase ASCII (punycode for non-Latin names).</summary>
    [MaxLength(253)]
    public string? CustomDomain { get; set; }

    /// <summary>Random token the owner publishes as a TXT record to prove they control the domain.</summary>
    [MaxLength(64)]
    public string? CustomDomainToken { get; set; }

    public DateTime? CustomDomainAddedAt { get; set; }

    /// <summary>When the ownership TXT record was first seen. Only verified domains route to the site.</summary>
    public DateTime? CustomDomainVerifiedAt { get; set; }

    /// <summary>When the domain first served the website (DNS + hosting + HTTPS all working).</summary>
    public DateTime? CustomDomainActiveAt { get; set; }

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
