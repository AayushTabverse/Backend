using menu_backend.DTOs;
using menu_backend.Helpers;
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
    [Authorize(Roles = Roles.AllStaff)]
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
    /// UPI payment QR for a bill: scanning it opens the customer's UPI app with the amount filled in.
    /// </summary>
    [HttpGet("upi-qr")]
    [Authorize(Roles = Roles.FloorStaff)]
    public async Task<IActionResult> GetUpiQr([FromQuery] decimal? amount, [FromQuery] string? note)
    {
        if (amount is < 0 or > 1_000_000)
            return BadRequest(ApiResponse.Fail("Invalid amount."));
        try
        {
            var png = await _settingsService.GenerateUpiQrAsync(amount, note);
            return File(png, "image/png");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
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
    [Authorize(Roles = Roles.Owner)]
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
