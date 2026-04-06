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
        if (request.PrinterWidth != null) tenant.PrinterWidth = request.PrinterWidth;
        if (request.DirectPrint != null) tenant.DirectPrint = request.DirectPrint.Value;
        if (request.CgstPercent != null) tenant.CgstPercent = request.CgstPercent.Value;
        if (request.SgstPercent != null) tenant.SgstPercent = request.SgstPercent.Value;
        if (request.ServiceChargePercent != null) tenant.ServiceChargePercent = request.ServiceChargePercent.Value;
        if (request.MaxDiscountPercent != null) tenant.MaxDiscountPercent = request.MaxDiscountPercent.Value;
        if (request.SpinWheelEnabled != null) tenant.SpinWheelEnabled = request.SpinWheelEnabled.Value;

        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapSettings(tenant);
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
        PrinterWidth = tenant.PrinterWidth,
        DirectPrint = tenant.DirectPrint,
        CgstPercent = tenant.CgstPercent,
        SgstPercent = tenant.SgstPercent,
        ServiceChargePercent = tenant.ServiceChargePercent,
        MaxDiscountPercent = tenant.MaxDiscountPercent,
        SpinWheelEnabled = tenant.SpinWheelEnabled
    };
}
