using menu_backend.DTOs.Analytics;

namespace menu_backend.Services.Interfaces;

public interface IAnalyticsService
{
    Task<DashboardSummaryResponse> GetDashboardSummaryAsync();
    Task<List<TopItemResponse>> GetTopItemsAsync(int count = 10, int days = 30);
    Task<List<SalesResponse>> GetSalesAsync(DateTime from, DateTime to);
    Task<List<PeakHoursResponse>> GetPeakHoursAsync(int days = 7);
}
