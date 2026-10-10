using menu_backend.DTOs.Analytics;

namespace menu_backend.Services.Interfaces;

public interface IAnalyticsInsightsService
{
    /// <summary>Everything for the Analytics page for a local date range, compared with the previous period of the same length.</summary>
    Task<AnalyticsOverviewResponse> GetOverviewAsync(DateOnly from, DateOnly to);

    /// <summary>Today so far vs the same weekday last week up to the same time, plus live status and alerts.</summary>
    Task<TodayDashboardResponse> GetTodayAsync();
}
