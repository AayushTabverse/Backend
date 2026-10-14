using menu_backend.DTOs;
using menu_backend.DTOs.Reservation;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Reservations)]
public class ReservationController : ControllerBase
{
    private const int MaxRequestsPerClient = 5;
    private static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(10);

    private readonly IReservationService _reservationService;
    private readonly IMemoryCache _cache;

    public ReservationController(IReservationService reservationService, IMemoryCache cache)
    {
        _reservationService = reservationService;
        _cache = cache;
    }

    /// <summary>
    /// Bookings on restaurant-local days from..to (default: today + 6 days), plus all pending requests.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReservations([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from ?? BusinessClock.Today.ToDateTime(TimeOnly.MinValue);
        var end = to ?? start.AddDays(6);
        if (start > end) (start, end) = (end, start);
        if ((end - start).TotalDays > 366)
            return BadRequest(ApiResponse.Fail("Choose a range of one year or less."));

        var result = await _reservationService.GetReservationsAsync(start, end);
        return Ok(ApiResponse<List<ReservationResponse>>.Ok(result));
    }

    [HttpGet("pending-count")]
    public async Task<IActionResult> GetPendingCount()
    {
        var count = await _reservationService.GetPendingCountAsync();
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateReservationRequest request) =>
        Run(() => _reservationService.CreateAsync(request), "Reservation saved.");

    [HttpPut("{id}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateReservationRequest request) =>
        Run(() => _reservationService.UpdateAsync(id, request), "Reservation updated.");

    [HttpPost("{id}/accept")]
    public Task<IActionResult> Accept(Guid id, [FromBody] AcceptReservationRequest request) =>
        Run(() => _reservationService.AcceptAsync(id, request), "Reservation confirmed.");

    [HttpPost("{id}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromBody] RejectReservationRequest request) =>
        Run(() => _reservationService.RejectAsync(id, request), "Request declined.");

    [HttpPost("{id}/cancel")]
    public Task<IActionResult> Cancel(Guid id) =>
        Run(() => _reservationService.CancelAsync(id), "Reservation cancelled.");

    [HttpPost("{id}/no-show")]
    public Task<IActionResult> NoShow(Guid id) =>
        Run(() => _reservationService.MarkNoShowAsync(id), "Marked as no-show.");

    /// <summary>Party arrived: seat them at one or more (merged) tables.</summary>
    [HttpPost("{id}/seat")]
    public Task<IActionResult> Seat(Guid id, [FromBody] SeatReservationRequest request) =>
        Run(() => _reservationService.SeatAsync(id, request), "Party seated.");

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _reservationService.GetSettingsAsync();
        return Ok(ApiResponse<ReservationSettingsResponse>.Ok(result));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateReservationSettingsRequest request)
    {
        var result = await _reservationService.UpdateSettingsAsync(request);
        return Ok(ApiResponse<ReservationSettingsResponse>.Ok(result, "Reservation settings saved."));
    }

    // ── Public (restaurant website, which may run on the restaurant's own domain) ──

    [HttpGet("public/{tenantId}/info")]
    [AllowAnonymous]
    [EnableCors("PublicForms")]
    public async Task<IActionResult> GetPublicInfo(string tenantId)
    {
        try
        {
            var result = await _reservationService.GetPublicInfoAsync(tenantId);
            return Ok(ApiResponse<PublicReservationInfoResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Customer requests a table from the website. The restaurant accepts or rejects it.</summary>
    [HttpPost("public/{tenantId}/request")]
    [AllowAnonymous]
    [EnableCors("PublicForms")]
    public async Task<IActionResult> RequestReservation(string tenantId, [FromBody] PublicReservationRequest request)
    {
        // Light abuse guard: a handful of requests per client per window
        var client = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                     ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"reservation-request-{tenantId}-{client}";
        var count = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = RequestWindow; return 0; });
        if (count >= MaxRequestsPerClient)
            return StatusCode(429, ApiResponse.Fail("Too many attempts. Please call the restaurant."));
        _cache.Set(key, count + 1, RequestWindow);

        return await RunPublic(() => _reservationService.RequestAsync(tenantId, request), "Request sent!");
    }

    /// <summary>Customer's booking page. The reservation id is the access key.</summary>
    [HttpGet("public/booking/{id}")]
    [AllowAnonymous]
    [EnableCors("PublicForms")]
    public Task<IActionResult> GetPublicStatus(Guid id) =>
        RunPublic(() => _reservationService.GetPublicStatusAsync(id));

    [HttpPost("public/booking/{id}/cancel")]
    [AllowAnonymous]
    [EnableCors("PublicForms")]
    public Task<IActionResult> CancelByCustomer(Guid id) =>
        RunPublic(() => _reservationService.CancelByCustomerAsync(id), "Booking cancelled.");

    private async Task<IActionResult> Run(Func<Task<ReservationResponse>> action, string message)
    {
        try
        {
            var result = await action();
            return Ok(ApiResponse<ReservationResponse>.Ok(result, message));
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

    private async Task<IActionResult> RunPublic(Func<Task<PublicReservationStatusResponse>> action, string? message = null)
    {
        try
        {
            var result = await action();
            return Ok(ApiResponse<PublicReservationStatusResponse>.Ok(result, message));
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
