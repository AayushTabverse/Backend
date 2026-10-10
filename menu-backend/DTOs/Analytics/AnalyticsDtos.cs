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
    public DateOnly Date { get; set; }
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
    public decimal TotalUnsettledDues { get; set; }
    public int UnsettledDueCount { get; set; }
    public decimal TodayDiscountGiven { get; set; }
    public List<TopItemResponse> TopItems { get; set; } = new();
}

// ═══════════════════════════════════════════════════
// Analytics overview: any date range vs the previous period of the same length
// ═══════════════════════════════════════════════════

/// <summary>A number with the same number for the previous period. ChangePct is null when there's nothing to compare.</summary>
public class Metric
{
    public decimal Value { get; set; }
    public decimal Previous { get; set; }
    public decimal? ChangePct { get; set; }

    public static Metric Of(decimal value, decimal previous) => new()
    {
        Value = Math.Round(value, 2),
        Previous = Math.Round(previous, 2),
        ChangePct = previous == 0 ? null : Math.Round((value - previous) / previous * 100, 1)
    };
}

public class AnalyticsOverviewResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PreviousFrom { get; set; }
    public DateOnly PreviousTo { get; set; }
    public int Days { get; set; }

    public Metric Revenue { get; set; } = new();
    public Metric Orders { get; set; } = new();
    public Metric AvgOrderValue { get; set; } = new();
    public Metric ItemsPerOrder { get; set; } = new();
    public Metric Discounts { get; set; } = new();
    /// <summary>Discounts as % of sales before discount.</summary>
    public Metric DiscountRate { get; set; } = new();
    public Metric CancelledOrders { get; set; } = new();
    /// <summary>Cancelled as % of all orders placed.</summary>
    public Metric CancelRate { get; set; } = new();
    public Metric Tax { get; set; } = new();

    /// <summary>One row per day of the range; Prev* is the matching day of the previous period.</summary>
    public List<DailyPoint> Daily { get; set; } = new();
    /// <summary>Orders by local weekday (0 = Monday) × hour.</summary>
    public List<HeatCell> Heatmap { get; set; } = new();
    public List<HourPoint> Hourly { get; set; } = new();
    public List<CategoryShare> Categories { get; set; } = new();
    public List<ItemPerformance> TopItems { get; set; } = new();
    public List<ItemPerformance> Rising { get; set; } = new();
    public List<ItemPerformance> Falling { get; set; } = new();
    /// <summary>Available menu items with no sales in the range.</summary>
    public List<string> NotSelling { get; set; } = new();
    public int NotSellingCount { get; set; }
    public KitchenPerformance Kitchen { get; set; } = new();
    public OrderSources Sources { get; set; } = new();
    public List<TablePerformance> Tables { get; set; } = new();
    public List<DueBucket> DuesAging { get; set; } = new();
    public List<Insight> Insights { get; set; } = new();
}

public class DailyPoint
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public DateOnly PrevDate { get; set; }
    public decimal PrevRevenue { get; set; }
    public int PrevOrders { get; set; }
}

public class HeatCell
{
    public int Weekday { get; set; }
    public int Hour { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}

public class HourPoint
{
    public int Hour { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
}

public class CategoryShare
{
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Quantity { get; set; }
    public decimal SharePct { get; set; }
}

public class ItemPerformance
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal SharePct { get; set; }
    public int PrevQuantity { get; set; }
    public decimal? ChangePct { get; set; }
}

public class KitchenPerformance
{
    /// <summary>Accepted → prepared, minutes (median is less skewed by forgotten orders than the mean).</summary>
    public Metric MedianPrepMinutes { get; set; } = new();
    /// <summary>Placed → served, minutes.</summary>
    public Metric MedianServeMinutes { get; set; } = new();
    /// <summary>Orders prepared within their estimated time, %.</summary>
    public Metric OnTimePct { get; set; } = new();
    public int Measured { get; set; }
}

public class OrderSources
{
    /// <summary>Placed by customers scanning the table QR code.</summary>
    public int QrOrders { get; set; }
    public decimal QrRevenue { get; set; }
    /// <summary>Taken by staff on the Take Order screen.</summary>
    public int StaffOrders { get; set; }
    public decimal StaffRevenue { get; set; }
}

public class TablePerformance
{
    public string Table { get; set; } = string.Empty;
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
    public decimal AvgOrderValue { get; set; }
}

public class DueBucket
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>A plain-language finding. Tone: good, bad or neutral.</summary>
public class Insight
{
    public string Icon { get; set; } = "💡";
    public string Text { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral";
}

// ═══════════════════════════════════════════════════
// Today dashboard: today so far vs the same weekday last week up to the same time
// ═══════════════════════════════════════════════════

public class TodayDashboardResponse
{
    public DateTime Now { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly CompareDate { get; set; }

    public Metric Revenue { get; set; } = new();
    public Metric Orders { get; set; } = new();
    public Metric AvgOrderValue { get; set; } = new();
    public Metric Discounts { get; set; } = new();

    /// <summary>Cumulative revenue by hour: today (up to now) and the same weekday last week (full day).</summary>
    public List<CumulativePoint> Cumulative { get; set; } = new();
    /// <summary>Revenue for each of the last 7 local days, oldest first.</summary>
    public List<DailyPoint> Last7Days { get; set; } = new();
    public decimal Last7Total { get; set; }

    public LiveStatus Live { get; set; } = new();
    public List<ItemPerformance> TopItems { get; set; } = new();
    public List<RecentOrder> RecentOrders { get; set; } = new();

    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal UnsettledDues { get; set; }
    public int UnsettledDueCount { get; set; }
    public int OldestDueDays { get; set; }
    public List<Insight> Insights { get; set; } = new();
}

public class CumulativePoint
{
    public int Hour { get; set; }
    /// <summary>Null for hours that haven't happened yet today.</summary>
    public decimal? Today { get; set; }
    public decimal LastWeek { get; set; }
}

public class LiveStatus
{
    public int Pending { get; set; }
    public int Preparing { get; set; }
    public int Ready { get; set; }
    public int Served { get; set; }
    public int OccupiedTables { get; set; }
    public int TotalTables { get; set; }
    /// <summary>Longest wait among orders not yet ready, minutes.</summary>
    public int OldestWaitMinutes { get; set; }
}

public class RecentOrder
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Items { get; set; }
    public DateTime CreatedAt { get; set; }
}
