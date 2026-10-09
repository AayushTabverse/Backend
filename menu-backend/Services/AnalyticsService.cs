using menu_backend.Data;
using menu_backend.DTOs.Analytics;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using menu_backend.Helpers;

namespace menu_backend.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync()
    {
        var (todayStart, todayEnd) = BusinessClock.DayRangeUtc(BusinessClock.Today);

        var todayOrders = await _db.Orders
            .Where(o => o.CreatedAt >= todayStart && o.CreatedAt < todayEnd && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var liveStatuses = new[] { OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready };

        var liveCount = await _db.Orders
            .CountAsync(o => liveStatuses.Contains(o.Status));

        var topItems = await GetTopItemsAsync(5, 1);

        // Due/Udhaar analytics
        var unsettledDues = await _db.CustomerDues
            .Where(d => !d.IsSettled)
            .ToListAsync();

        var totalUnsettledDues = unsettledDues.Sum(d => d.DueAmount);
        var unsettledDueCount = unsettledDues.Count;

        // Today's discount
        var todayDiscount = todayOrders.Sum(o => o.DiscountAmount);

        return new DashboardSummaryResponse
        {
            TodaySales = todayOrders.Sum(o => o.TotalAmount),
            TodayOrderCount = todayOrders.Count,
            LiveOrderCount = liveCount,
            AvgOrderValue = todayOrders.Count > 0 ? todayOrders.Average(o => o.TotalAmount) : 0,
            TotalUnsettledDues = totalUnsettledDues,
            UnsettledDueCount = unsettledDueCount,
            TodayDiscountGiven = todayDiscount,
            TopItems = topItems
        };
    }

    public async Task<List<TopItemResponse>> GetTopItemsAsync(int count = 10, int days = 30)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        return await _db.OrderItems
            .Where(oi => oi.CreatedAt >= fromDate)
            .GroupBy(oi => new { oi.MenuItemId, oi.ItemName })
            .Select(g => new TopItemResponse
            {
                MenuItemId = g.Key.MenuItemId,
                ItemName = g.Key.ItemName,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<SalesResponse>> GetSalesAsync(DateTime from, DateTime to)
    {
        // from/to are restaurant-local calendar days; group by local day, not UTC day
        var (start, end) = BusinessClock.DayRangeUtc(from, to);

        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= start && o.CreatedAt < end && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.CreatedAt, o.TotalAmount })
            .ToListAsync();

        return orders
            .GroupBy(o => DateOnly.FromDateTime(BusinessClock.ToLocal(o.CreatedAt)))
            .Select(g => new SalesResponse
            {
                Date = g.Key,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToList();
    }

    public async Task<List<PeakHoursResponse>> GetPeakHoursAsync(int days = 7)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= fromDate && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.CreatedAt, o.TotalAmount })
            .ToListAsync();

        // Group by the restaurant's local hour, not the UTC hour
        return orders
            .GroupBy(o => BusinessClock.ToLocal(o.CreatedAt).Hour)
            .Select(g => new PeakHoursResponse
            {
                Hour = g.Key,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Hour)
            .ToList();
    }
}
