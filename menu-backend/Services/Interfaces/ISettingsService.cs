using menu_backend.DTOs.Settings;

namespace menu_backend.Services.Interfaces;

public interface ISettingsService
{
    Task<BusinessSettingsResponse> GetSettingsAsync();
    Task<BusinessSettingsResponse> GetSettingsByTenantIdAsync(string tenantId);
    Task<BusinessSettingsResponse> UpdateSettingsAsync(UpdateBusinessSettingsRequest request);

    /// <summary>PNG QR for paying the restaurant's UPI ID, with the amount pre-filled when given.</summary>
    Task<byte[]> GenerateUpiQrAsync(decimal? amount, string? note);
}
