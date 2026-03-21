namespace menu_backend.DTOs.Analytics;

public class TopItemResponse
{
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class SalesResponse
{
    public DateTime Date { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
}

public class PeakHoursResponse
{
    public int Hour { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
}

public class DashboardSummaryResponse
{
    public decimal TodaySales { get; set; }
    public int TodayOrderCount { get; set; }
    public int LiveOrderCount { get; set; }
    public decimal AvgOrderValue { get; set; }
    public List<TopItemResponse> TopItems { get; set; } = new();
}
