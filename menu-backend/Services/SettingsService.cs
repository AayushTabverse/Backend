using menu_backend.Helpers;
using menu_backend.Data;
using menu_backend.DTOs.Settings;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public SettingsService(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<BusinessSettingsResponse> GetSettingsAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        return await GetSettingsByTenantIdAsync(tenantId);
    }

    public async Task<BusinessSettingsResponse> GetSettingsByTenantIdAsync(string tenantId)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        return MapSettings(tenant);
    }

    public async Task<BusinessSettingsResponse> UpdateSettingsAsync(UpdateBusinessSettingsRequest request)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        if (request.Name != null) tenant.Name = request.Name;
        if (request.Address != null) tenant.Address = request.Address;
        if (request.Phone != null) tenant.Phone = request.Phone;
        if (request.Email != null) tenant.Email = request.Email;
        if (request.LogoUrl != null) tenant.LogoUrl = request.LogoUrl;
        if (request.InstagramUrl != null) tenant.InstagramUrl = request.InstagramUrl;
        if (request.FacebookUrl != null) tenant.FacebookUrl = request.FacebookUrl;
        if (request.TwitterUrl != null) tenant.TwitterUrl = request.TwitterUrl;
        if (request.WebsiteUrl != null) tenant.WebsiteUrl = request.WebsiteUrl;
        if (request.GoogleMapsUrl != null) tenant.GoogleMapsUrl = request.GoogleMapsUrl;
        if (request.UpiQrCodeUrl != null) tenant.UpiQrCodeUrl = request.UpiQrCodeUrl;
        if (request.UpiId != null) tenant.UpiId = string.IsNullOrWhiteSpace(request.UpiId) ? null : request.UpiId.Trim();
        if (request.UpiPayeeName != null) tenant.UpiPayeeName = string.IsNullOrWhiteSpace(request.UpiPayeeName) ? null : request.UpiPayeeName.Trim();
        if (request.PrinterWidth != null) tenant.PrinterWidth = request.PrinterWidth;
        if (request.DirectPrint != null) tenant.DirectPrint = request.DirectPrint.Value;
        if (request.CgstPercent != null) tenant.CgstPercent = request.CgstPercent.Value;
        if (request.SgstPercent != null) tenant.SgstPercent = request.SgstPercent.Value;
        if (request.ServiceChargePercent != null) tenant.ServiceChargePercent = request.ServiceChargePercent.Value;
        if (request.MaxDiscountPercent != null) tenant.MaxDiscountPercent = request.MaxDiscountPercent.Value;
        if (request.SpinWheelEnabled != null) tenant.SpinWheelEnabled = request.SpinWheelEnabled.Value;
        if (request.LoyaltyDailyEarnCap != null) tenant.LoyaltyDailyEarnCap = request.LoyaltyDailyEarnCap.Value;
        if (request.LoyaltyBirthdayBonus != null) tenant.LoyaltyBirthdayBonus = request.LoyaltyBirthdayBonus.Value;
        if (request.LoyaltyWelcomeBonus != null) tenant.LoyaltyWelcomeBonus = request.LoyaltyWelcomeBonus.Value;
        if (request.LoyaltyExpiryMonths != null) tenant.LoyaltyExpiryMonths = request.LoyaltyExpiryMonths.Value;
        if (request.LoyaltyMaxRedeemPercent != null) tenant.LoyaltyMaxRedeemPercent = request.LoyaltyMaxRedeemPercent.Value;
        if (request.LoyaltyMinRedeemPoints != null) tenant.LoyaltyMinRedeemPoints = request.LoyaltyMinRedeemPoints.Value;
        if (request.LoyaltyPointValue != null) tenant.LoyaltyPointValue = request.LoyaltyPointValue.Value;
        if (request.LoyaltyPointsPer100 != null) tenant.LoyaltyPointsPer100 = request.LoyaltyPointsPer100.Value;
        if (request.LoyaltyEnabled != null) tenant.LoyaltyEnabled = request.LoyaltyEnabled.Value;
        if (request.HotelModeEnabled != null) tenant.HotelModeEnabled = request.HotelModeEnabled.Value;
        if (request.GeofenceRadiusMeters != null) tenant.GeofenceRadiusMeters = request.GeofenceRadiusMeters.Value;
        if (request.Longitude != null) tenant.Longitude = request.Longitude;
        if (request.Latitude != null) tenant.Latitude = request.Latitude;
        if (request.LastOrderMinutes != null) tenant.LastOrderMinutes = request.LastOrderMinutes.Value;
        if (request.QrOrderingHoursEnabled != null) tenant.QrOrderingHoursEnabled = request.QrOrderingHoursEnabled.Value;
        if (request.TakeawayPackingFee != null) tenant.TakeawayPackingFee = request.TakeawayPackingFee.Value;
        if (request.TakeawayEnabled != null) tenant.TakeawayEnabled = request.TakeawayEnabled.Value;
        if (request.KotCopies != null) tenant.KotCopies = request.KotCopies.Value;
        if (request.KotPrintTrigger is "OnOrder" or "OnAccept") tenant.KotPrintTrigger = request.KotPrintTrigger;
        if (request.AutoPrintKot != null) tenant.AutoPrintKot = request.AutoPrintKot.Value;

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapSettings(tenant);
    }

    public async Task<BusinessSettingsResponse> SetOrderingOverrideAsync(int minutes)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        tenant.OrderingOpenUntil = minutes > 0 ? DateTime.UtcNow.AddMinutes(Math.Min(minutes, 24 * 60)) : null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapSettings(tenant);
    }

    public async Task<byte[]> GenerateUpiQrAsync(decimal? amount, string? note)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");
        if (string.IsNullOrWhiteSpace(tenant.UpiId))
            throw new InvalidOperationException("Add your UPI ID in Settings to print payment QRs with the bill amount.");

        var link = UpiLink.Build(tenant.UpiId, tenant.UpiPayeeName ?? tenant.Name, amount, note);

        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(link, QRCoder.QRCodeGenerator.ECCLevel.M);
        using var qr = new QRCoder.PngByteQRCode(data);
        return qr.GetGraphic(10);
    }

    private static BusinessSettingsResponse MapSettings(Models.Tenant tenant) => new()
    {
        Name = tenant.Name,
        Address = tenant.Address,
        Phone = tenant.Phone,
        Email = tenant.Email,
        LogoUrl = tenant.LogoUrl,
        InstagramUrl = tenant.InstagramUrl,
        FacebookUrl = tenant.FacebookUrl,
        TwitterUrl = tenant.TwitterUrl,
        WebsiteUrl = tenant.WebsiteUrl,
        GoogleMapsUrl = tenant.GoogleMapsUrl,
        CurrencyCode = tenant.CurrencyCode,
        UpiQrCodeUrl = tenant.UpiQrCodeUrl,
        UpiId = tenant.UpiId,
        UpiPayeeName = tenant.UpiPayeeName,
        PrinterWidth = tenant.PrinterWidth,
        DirectPrint = tenant.DirectPrint,
        CgstPercent = tenant.CgstPercent,
        SgstPercent = tenant.SgstPercent,
        ServiceChargePercent = tenant.ServiceChargePercent,
        MaxDiscountPercent = tenant.MaxDiscountPercent,
        SpinWheelEnabled = tenant.SpinWheelEnabled,
        LoyaltyDailyEarnCap = tenant.LoyaltyDailyEarnCap,
        LoyaltyBirthdayBonus = tenant.LoyaltyBirthdayBonus,
        LoyaltyWelcomeBonus = tenant.LoyaltyWelcomeBonus,
        LoyaltyExpiryMonths = tenant.LoyaltyExpiryMonths,
        LoyaltyMaxRedeemPercent = tenant.LoyaltyMaxRedeemPercent,
        LoyaltyMinRedeemPoints = tenant.LoyaltyMinRedeemPoints,
        LoyaltyPointValue = tenant.LoyaltyPointValue,
        LoyaltyPointsPer100 = tenant.LoyaltyPointsPer100,
        LoyaltyEnabled = tenant.LoyaltyEnabled,
        HotelModeEnabled = tenant.HotelModeEnabled,
        OrderingOpenUntil = tenant.OrderingOpenUntil,
        GeofenceRadiusMeters = tenant.GeofenceRadiusMeters,
        Longitude = tenant.Longitude,
        Latitude = tenant.Latitude,
        LastOrderMinutes = tenant.LastOrderMinutes,
        QrOrderingHoursEnabled = tenant.QrOrderingHoursEnabled,
        TakeawayPackingFee = tenant.TakeawayPackingFee,
        TakeawayEnabled = tenant.TakeawayEnabled,
        KotCopies = tenant.KotCopies,
        KotPrintTrigger = tenant.KotPrintTrigger,
        AutoPrintKot = tenant.AutoPrintKot
    };
}
