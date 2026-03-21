using menu_backend.DTOs;
using menu_backend.DTOs.Analytics;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _analyticsService.GetDashboardSummaryAsync();
        return Ok(ApiResponse<DashboardSummaryResponse>.Ok(result));
    }

    [HttpGet("top-items")]
    public async Task<IActionResult> GetTopItems([FromQuery] int count = 10, [FromQuery] int days = 30)
    {
        var result = await _analyticsService.GetTopItemsAsync(count, days);
        return Ok(ApiResponse<List<TopItemResponse>>.Ok(result));
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await _analyticsService.GetSalesAsync(from, to);
        return Ok(ApiResponse<List<SalesResponse>>.Ok(result));
    }

    [HttpGet("peak-hours")]
    public async Task<IActionResult> GetPeakHours([FromQuery] int days = 7)
    {
        var result = await _analyticsService.GetPeakHoursAsync(days);
        return Ok(ApiResponse<List<PeakHoursResponse>>.Ok(result));
    }
}
