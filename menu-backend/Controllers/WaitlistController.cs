using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Waitlist;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Waitlist)]
public class WaitlistController : ControllerBase
{
    private const int MaxJoinsPerClient = 5;
    private static readonly TimeSpan JoinWindow = TimeSpan.FromMinutes(10);

    private readonly IWaitlistService _waitlistService;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    public WaitlistController(IWaitlistService waitlistService, IConfiguration config, IMemoryCache cache)
    {
        _waitlistService = waitlistService;
        _config = config;
        _cache = cache;
    }

    /// <summary>
    /// Open parties (waiting, called, seated), plus parties closed today when includeClosed=true.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEntries([FromQuery] bool includeClosed = false)
    {
        var result = await _waitlistService.GetEntriesAsync(includeClosed);
        return Ok(ApiResponse<List<WaitlistEntryResponse>>.Ok(result));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _waitlistService.GetStatsAsync();
        return Ok(ApiResponse<WaitlistStatsResponse>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> AddEntry([FromBody] CreateWaitlistEntryRequest request)
    {
        var result = await _waitlistService.AddEntryAsync(request);
        return Ok(ApiResponse<WaitlistEntryResponse>.Ok(result, $"Added to waitlist as #{result.TokenNumber}."));
    }

    [HttpPut("{id}")]
    public Task<IActionResult> UpdateEntry(Guid id, [FromBody] UpdateWaitlistEntryRequest request) =>
        Run(() => _waitlistService.UpdateEntryAsync(id, request));

    /// <summary>Mark the party as called (staff phoned / WhatsApped them).</summary>
    [HttpPost("{id}/notify")]
    public Task<IActionResult> Notify(Guid id) =>
        Run(() => _waitlistService.NotifyAsync(id), "Marked as called.");

    /// <summary>Seat the party at one table, or several merged tables.</summary>
    [HttpPost("{id}/seat")]
    public Task<IActionResult> Seat(Guid id, [FromBody] SeatWaitlistEntryRequest request) =>
        Run(() => _waitlistService.SeatAsync(id, request), "Party seated.");

    /// <summary>Free the tables held by a seated party (e.g. they left without ordering).</summary>
    [HttpPost("{id}/release")]
    public Task<IActionResult> Release(Guid id) =>
        Run(() => _waitlistService.ReleaseAsync(id), "Tables released.");

    [HttpPost("{id}/cancel")]
    public Task<IActionResult> Cancel(Guid id) =>
        Run(() => _waitlistService.CancelAsync(id), "Removed from waitlist.");

    [HttpPost("{id}/no-show")]
    public Task<IActionResult> NoShow(Guid id) =>
        Run(() => _waitlistService.MarkNoShowAsync(id), "Marked as no-show.");

    // ── Settings, QR, analytics ──

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _waitlistService.GetSettingsAsync();
        return Ok(ApiResponse<WaitlistSettingsResponse>.Ok(result));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateWaitlistSettingsRequest request)
    {
        var result = await _waitlistService.UpdateSettingsAsync(request);
        return Ok(ApiResponse<WaitlistSettingsResponse>.Ok(result, "Waitlist settings saved."));
    }

    /// <summary>
    /// QR for the entrance: customers scan it to join the waitlist themselves.
    /// </summary>
    [HttpGet("qr")]
    public IActionResult GetCheckInQr([FromServices] ITenantProvider tenantProvider)
    {
        var baseUrl = _config["App:BaseUrl"] ?? "https://yourapp.com";
        var url = $"{baseUrl}/waitlist/join/{tenantProvider.TenantId}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return File(qr.GetGraphic(20), "image/png", "waitlist-checkin-qr.png");
    }

    /// <summary>
    /// Waitlist analytics over restaurant-local days from..to (inclusive). Defaults to the last 30 days.
    /// </summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var today = BusinessClock.Today.ToDateTime(TimeOnly.MinValue);
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);
        if (start > end) (start, end) = (end, start);
        if ((end - start).TotalDays > 366)
            return BadRequest(ApiResponse.Fail("Choose a range of one year or less."));

        var result = await _waitlistService.GetAnalyticsAsync(start, end);
        return Ok(ApiResponse<WaitlistAnalyticsResponse>.Ok(result));
    }

    // ── Public (customer-facing) ──

    /// <summary>Restaurant name, queue length and expected wait for the self check-in page.</summary>
    [HttpGet("public/{tenantId}/info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicInfo(string tenantId)
    {
        try
        {
            var result = await _waitlistService.GetPublicInfoAsync(tenantId);
            return Ok(ApiResponse<PublicWaitlistInfoResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Customer adds themselves to the waitlist (entrance QR).</summary>
    [HttpPost("public/{tenantId}/join")]
    [AllowAnonymous]
    public async Task<IActionResult> Join(string tenantId, [FromBody] PublicJoinWaitlistRequest request)
    {
        // Light abuse guard: a handful of joins per client per window
        var client = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                     ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"waitlist-join-{tenantId}-{client}";
        var count = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = JoinWindow; return 0; });
        if (count >= MaxJoinsPerClient)
            return StatusCode(429, ApiResponse.Fail("Too many attempts. Please ask the staff at the entrance."));
        _cache.Set(key, count + 1, JoinWindow);

        return await RunPublic(() => _waitlistService.JoinAsync(tenantId, request), "You're on the waitlist!");
    }

    /// <summary>Customer's live status page (link sent on WhatsApp). The entry id is the access key.</summary>
    [HttpGet("public/entry/{id}")]
    [AllowAnonymous]
    public Task<IActionResult> GetPublicStatus(Guid id) =>
        RunPublic(() => _waitlistService.GetPublicStatusAsync(id));

    /// <summary>Customer leaves the line from their status page.</summary>
    [HttpPost("public/entry/{id}/leave")]
    [AllowAnonymous]
    public Task<IActionResult> Leave(Guid id) =>
        RunPublic(() => _waitlistService.LeaveAsync(id), "You've left the waitlist.");

    private async Task<IActionResult> RunPublic(Func<Task<PublicWaitlistStatusResponse>> action, string? message = null)
    {
        try
        {
            var result = await action();
            return Ok(ApiResponse<PublicWaitlistStatusResponse>.Ok(result, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private async Task<IActionResult> Run(Func<Task<WaitlistEntryResponse>> action, string? message = null)
    {
        try
        {
            var result = await action();
            return Ok(ApiResponse<WaitlistEntryResponse>.Ok(result, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
