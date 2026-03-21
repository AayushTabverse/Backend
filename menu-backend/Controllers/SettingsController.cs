using menu_backend.DTOs;
using menu_backend.DTOs.Settings;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Get business settings for the current tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var result = await _settingsService.GetSettingsAsync();
            return Ok(ApiResponse<BusinessSettingsResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get business settings by tenantId (public — for customer menu).
    /// </summary>
    [HttpGet("public/{tenantId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicSettings(string tenantId)
    {
        try
        {
            var result = await _settingsService.GetSettingsByTenantIdAsync(tenantId);
            return Ok(ApiResponse<BusinessSettingsResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update business settings.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateBusinessSettingsRequest request)
    {
        try
        {
            var result = await _settingsService.UpdateSettingsAsync(request);
            return Ok(ApiResponse<BusinessSettingsResponse>.Ok(result, "Settings updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
