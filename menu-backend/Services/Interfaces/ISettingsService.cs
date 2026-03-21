using menu_backend.DTOs.Settings;

namespace menu_backend.Services.Interfaces;

public interface ISettingsService
{
    Task<BusinessSettingsResponse> GetSettingsAsync();
    Task<BusinessSettingsResponse> GetSettingsByTenantIdAsync(string tenantId);
    Task<BusinessSettingsResponse> UpdateSettingsAsync(UpdateBusinessSettingsRequest request);
}
