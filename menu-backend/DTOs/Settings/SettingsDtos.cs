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
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }
    public string? PrinterWidth { get; set; }
    public bool DirectPrint { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal MaxDiscountPercent { get; set; }
    public bool SpinWheelEnabled { get; set; }
    public int LoyaltyDailyEarnCap { get; set; }
    public int LoyaltyBirthdayBonus { get; set; }
    public int LoyaltyWelcomeBonus { get; set; }
    public int LoyaltyExpiryMonths { get; set; }
    public decimal LoyaltyMaxRedeemPercent { get; set; }
    public int LoyaltyMinRedeemPoints { get; set; }
    public decimal LoyaltyPointValue { get; set; }
    public decimal LoyaltyPointsPer100 { get; set; }
    public bool LoyaltyEnabled { get; set; }
    public bool HotelModeEnabled { get; set; }
    public DateTime? OrderingOpenUntil { get; set; }
    public int GeofenceRadiusMeters { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public int LastOrderMinutes { get; set; }
    public bool QrOrderingHoursEnabled { get; set; }
    public decimal TakeawayPackingFee { get; set; }
    public bool TakeawayEnabled { get; set; }
    public int KotCopies { get; set; }
    public string KotPrintTrigger { get; set; } = "OnOrder";
    public bool AutoPrintKot { get; set; }
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

    /// <summary>UPI ID (VPA). Empty string clears it.</summary>
    [MaxLength(100)]
    [RegularExpression(@"^$|^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z][a-zA-Z0-9.\-]{1,63}$", ErrorMessage = "Enter a valid UPI ID, like yourshop@okicici.")]
    public string? UpiId { get; set; }

    [MaxLength(100)]
    public string? UpiPayeeName { get; set; }

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

    [Range(0, 100000)]
    public int? LoyaltyDailyEarnCap { get; set; }

    [Range(0, 100000)]
    public int? LoyaltyBirthdayBonus { get; set; }

    [Range(0, 100000)]
    public int? LoyaltyWelcomeBonus { get; set; }

    [Range(0, 120)]
    public int? LoyaltyExpiryMonths { get; set; }

    [Range(0, 100)]
    public decimal? LoyaltyMaxRedeemPercent { get; set; }

    [Range(0, 100000)]
    public int? LoyaltyMinRedeemPoints { get; set; }

    [Range(0.01, 100)]
    public decimal? LoyaltyPointValue { get; set; }

    [Range(0, 100)]
    public decimal? LoyaltyPointsPer100 { get; set; }

    public bool? LoyaltyEnabled { get; set; }

    public bool? HotelModeEnabled { get; set; }

    [Range(0, 5000)]
    public int? GeofenceRadiusMeters { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(0, 240)]
    public int? LastOrderMinutes { get; set; }

    public bool? QrOrderingHoursEnabled { get; set; }

    [Range(0, 10000)]
    public decimal? TakeawayPackingFee { get; set; }

    public bool? TakeawayEnabled { get; set; }

    [Range(1, 5)]
    public int? KotCopies { get; set; }

    [MaxLength(20)]
    public string? KotPrintTrigger { get; set; }

    public bool? AutoPrintKot { get; set; }
}