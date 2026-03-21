using menu_backend.Data;
using menu_backend.DTOs.Analytics;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        var today = DateTime.UtcNow.Date;

        var todayOrders = await _db.Orders
            .Where(o => o.CreatedAt.Date == today && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var liveStatuses = new[] { OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready };

        var liveCount = await _db.Orders
            .CountAsync(o => liveStatuses.Contains(o.Status));

        var topItems = await GetTopItemsAsync(5, 1);

        return new DashboardSummaryResponse
        {
            TodaySales = todayOrders.Sum(o => o.TotalAmount),
            TodayOrderCount = todayOrders.Count,
            LiveOrderCount = liveCount,
            AvgOrderValue = todayOrders.Count > 0 ? todayOrders.Average(o => o.TotalAmount) : 0,
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
        return await _db.Orders
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new SalesResponse
            {
                Date = g.Key,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();
    }

    public async Task<List<PeakHoursResponse>> GetPeakHoursAsync(int days = 7)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        return await _db.Orders
            .Where(o => o.CreatedAt >= fromDate && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new PeakHoursResponse
            {
                Hour = g.Key,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Hour)
            .ToListAsync();
    }
}
