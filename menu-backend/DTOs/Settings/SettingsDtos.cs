using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Settings;

public class BusinessSettingsResponse
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public string? CurrencyCode { get; set; }
    public string? UpiQrCodeUrl { get; set; }
    public string? PrinterWidth { get; set; }
    public bool DirectPrint { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal MaxDiscountPercent { get; set; }
    public bool SpinWheelEnabled { get; set; }
}

public class UpdateBusinessSettingsRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

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
    public string? PrinterWidth { get; set; }

    public bool? DirectPrint { get; set; }

    [Range(0, 100)]
    public decimal? CgstPercent { get; set; }

    [Range(0, 100)]
    public decimal? SgstPercent { get; set; }

    [Range(0, 100)]
    public decimal? ServiceChargePercent { get; set; }

    [Range(0, 100)]
    public decimal? MaxDiscountPercent { get; set; }

    public bool? SpinWheelEnabled { get; set; }
}