using menu_backend.DTOs;
using menu_backend.DTOs.Print;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

/// <summary>KOT queue for the Print Station page (a browser kept open at the counter).</summary>
[ApiController]
[Route("api/[controller]")]
// Roles are set per endpoint (a class-level role list would also apply to, and block, wider endpoints)
[Authorize]
public class PrintController : ControllerBase
{
    private readonly IPrintService _printService;

    public PrintController(IPrintService printService)
    {
        _printService = printService;
    }

    [HttpGet("pending")]
    [Authorize(Roles = Roles.Billing)]
    public async Task<IActionResult> GetPending()
    {
        var result = await _printService.GetPendingAsync();
        return Ok(ApiResponse<List<KotTicketResponse>>.Ok(result));
    }

    /// <summary>The station takes a job before printing it. 409 when another station already has it.</summary>
    [HttpPut("{id}/claim")]
    [Authorize(Roles = Roles.Billing)]
    public async Task<IActionResult> Claim(Guid id)
    {
        return await _printService.ClaimAsync(id)
            ? Ok(ApiResponse.Ok())
            : Conflict(ApiResponse.Fail("Another Print Station is printing this KOT."));
    }

    [HttpPut("{id}/printed")]
    [Authorize(Roles = Roles.Billing)]
    public async Task<IActionResult> MarkPrinted(Guid id)
    {
        try
        {
            await _printService.MarkPrintedAsync(id);
            return Ok(ApiResponse.Ok("Print job marked as completed."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/failed")]
    [Authorize(Roles = Roles.Billing)]
    public async Task<IActionResult> MarkFailed(Guid id, [FromQuery] string error = "Unknown error")
    {
        try
        {
            await _printService.MarkFailedAsync(id, error);
            return Ok(ApiResponse.Ok("Print job marked as failed."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Print this order's KOT again (lost slip, printer jam).</summary>
    [HttpPost("order/{orderId}/kot")]
    [Authorize(Roles = Roles.AllStaff)]
    public async Task<IActionResult> ReprintKot(Guid orderId)
    {
        try
        {
            await _printService.ReprintKotAsync(orderId);
            return Ok(ApiResponse.Ok("KOT sent to the Print Station."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
