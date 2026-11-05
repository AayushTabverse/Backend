using System.Security.Claims;
using menu_backend.DTOs;
using menu_backend.DTOs.Notification;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Management)]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Bell count and the alerts that blink on admin screens.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _notificationService.GetSummaryAsync();
        return Ok(ApiResponse<NotificationSummaryResponse>.Ok(summary));
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] string filter = "open", [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var result = await _notificationService.GetNotificationsAsync(filter, page, pageSize);
        return Ok(ApiResponse<PaginatedNotificationsResponse>.Ok(result));
    }

    [HttpPut("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        try
        {
            await _notificationService.AcknowledgeAsync(id, User.FindFirstValue(ClaimTypes.Name));
            return Ok(ApiResponse.Ok());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("acknowledge-all")]
    public async Task<IActionResult> AcknowledgeAll()
    {
        var count = await _notificationService.AcknowledgeAllAsync(User.FindFirstValue(ClaimTypes.Name));
        return Ok(ApiResponse.Ok($"{count} notification{(count == 1 ? "" : "s")} acknowledged."));
    }
}
