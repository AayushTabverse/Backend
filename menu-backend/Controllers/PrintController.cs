using menu_backend.DTOs;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class PrintController : ControllerBase
{
    private readonly IPrintService _printService;

    public PrintController(IPrintService printService)
    {
        _printService = printService;
    }

    /// <summary>
    /// Get pending print jobs (polled by local print agent).
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var result = await _printService.GetPendingPrintJobsAsync();
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>
    /// Mark print job as completed.
    /// </summary>
    [HttpPut("{id}/printed")]
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

    /// <summary>
    /// Mark print job as failed.
    /// </summary>
    [HttpPut("{id}/failed")]
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
}
