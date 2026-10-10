using menu_backend.DTOs;
using menu_backend.Helpers;
using menu_backend.DTOs.Analytics;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Management)]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsInsightsService _insights;

    public AnalyticsController(IAnalyticsService analyticsService, IAnalyticsInsightsService insights)
    {
        _analyticsService = analyticsService;
        _insights = insights;
    }

    /// <summary>Today so far vs the same weekday last week, live status and alerts (Dashboard).</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        return Ok(ApiResponse<TodayDashboardResponse>.Ok(await _insights.GetTodayAsync()));
    }

    /// <summary>Analytics for local days from..to (inclusive), compared with the previous period of the same length.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(ApiResponse<AnalyticsOverviewResponse>.Ok(await _insights.GetOverviewAsync(from, to)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
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
