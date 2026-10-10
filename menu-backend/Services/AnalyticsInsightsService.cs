using menu_backend.Data;
using menu_backend.DTOs.Analytics;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

/// <summary>
/// Dashboard ("today so far") and Analytics (any range vs the previous period of equal length).
/// All day/hour grouping uses the restaurant's local time (BusinessClock). Revenue counts every
/// order that wasn't cancelled, matching the existing dashboard and bills.
/// </summary>
public class AnalyticsInsightsService : IAnalyticsInsightsService
{
    public const int MaxRangeDays = 366;
    private static readonly System.Globalization.CultureInfo India = new("en-IN");

    /// <summary>₹ with Indian digit grouping (₹4,70,834) regardless of the server's culture.</summary>
    private static string Rs(decimal v) => "₹" + v.ToString("N0", India);
    private static readonly string[] WeekdayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private readonly AppDbContext _db;

    public AnalyticsInsightsService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Lightweight order row: everything the analytics need, nothing else.</summary>
    private sealed record OrderRow(
        Guid Id, string OrderNumber, DateTime CreatedAt, DateTime Local, OrderStatus Status,
        decimal Total, decimal Discount, decimal Tax,
        DateTime? AcceptedAt, DateTime? PreparedAt, DateTime? ServedAt, int EstimatedMinutes,
        bool ViaQr, Guid TableId, string Table, List<ItemRow> Items)
    {
        public bool Counts => Status != OrderStatus.Cancelled;
        public DateOnly Day => DateOnly.FromDateTime(Local);
    }

    private sealed record ItemRow(Guid MenuItemId, string Name, int Quantity, decimal Revenue);

    private async Task<List<OrderRow>> LoadOrdersAsync(DateTime startUtc, DateTime endUtc)
    {
        var rows = await _db.Orders
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CreatedAt, o.Status, o.TotalAmount, o.DiscountAmount, o.Tax,
                o.AcceptedAt, o.PreparedAt, o.ServedAt, o.EstimatedMinutes, o.CustomerSessionId, o.TableId,
                Table = o.Table != null ? o.Table.TableNumber : "",
                Items = o.Items.Where(i => !i.IsDeleted).Select(i => new { i.MenuItemId, i.ItemName, i.Quantity, i.TotalPrice }).ToList()
            })
            .ToListAsync();

        return rows.Select(o => new OrderRow(
            o.Id, o.OrderNumber, o.CreatedAt, BusinessClock.ToLocal(o.CreatedAt), o.Status,
            o.TotalAmount, o.DiscountAmount, o.Tax, o.AcceptedAt, o.PreparedAt, o.ServedAt, o.EstimatedMinutes,
            o.CustomerSessionId != null, o.TableId, o.Table,
            o.Items.Select(i => new ItemRow(i.MenuItemId, i.ItemName, i.Quantity, i.TotalPrice)).ToList()
        )).ToList();
    }

    // ═══════════════════════════════════════════════════
    // Analytics overview
    // ═══════════════════════════════════════════════════

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            throw new ArgumentException($"Choose a range of at most {MaxRangeDays} days.");

        var days = to.DayNumber - from.DayNumber + 1;
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(days - 1));

        var (startUtc, endUtc) = BusinessClock.DayRangeUtc(prevFrom.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));
        var all = await LoadOrdersAsync(startUtc, endUtc);
        var cur = all.Where(o => o.Day >= from && o.Day <= to).ToList();
        var prev = all.Where(o => o.Day >= prevFrom && o.Day <= prevTo).ToList();
        var curOk = cur.Where(o => o.Counts).ToList();
        var prevOk = prev.Where(o => o.Counts).ToList();

        var menu = await _db.MenuItems
            .Select(m => new { m.Id, m.Name, m.IsAvailable, m.CreatedAt, Category = m.Category != null ? m.Category.Name : null })
            .ToListAsync();
        var categoryOf = menu.ToDictionary(m => m.Id, m => m.Category);

        var r = new AnalyticsOverviewResponse { From = from, To = to, PreviousFrom = prevFrom, PreviousTo = prevTo, Days = days };

        // ── KPIs ──
        decimal Sum(IEnumerable<OrderRow> s, Func<OrderRow, decimal> f) => s.Sum(f);
        decimal Avg(decimal total, int count) => count == 0 ? 0 : total / count;
        decimal Pct(decimal part, decimal whole) => whole == 0 ? 0 : part / whole * 100;

        var revenue = Sum(curOk, o => o.Total);
        var prevRevenue = Sum(prevOk, o => o.Total);
        var discounts = Sum(curOk, o => o.Discount);
        var prevDiscounts = Sum(prevOk, o => o.Discount);
        var items = curOk.Sum(o => o.Items.Sum(i => i.Quantity));
        var prevItems = prevOk.Sum(o => o.Items.Sum(i => i.Quantity));
        var cancelled = cur.Count(o => !o.Counts);
        var prevCancelled = prev.Count(o => !o.Counts);

        r.Revenue = Metric.Of(revenue, prevRevenue);
        r.Orders = Metric.Of(curOk.Count, prevOk.Count);
        r.AvgOrderValue = Metric.Of(Avg(revenue, curOk.Count), Avg(prevRevenue, prevOk.Count));
        r.ItemsPerOrder = Metric.Of(Avg(items, curOk.Count), Avg(prevItems, prevOk.Count));
        r.Discounts = Metric.Of(discounts, prevDiscounts);
        r.DiscountRate = Metric.Of(Pct(discounts, revenue + discounts), Pct(prevDiscounts, prevRevenue + prevDiscounts));
        r.CancelledOrders = Metric.Of(cancelled, prevCancelled);
        r.CancelRate = Metric.Of(Pct(cancelled, cur.Count), Pct(prevCancelled, prev.Count));
        r.Tax = Metric.Of(Sum(curOk, o => o.Tax), Sum(prevOk, o => o.Tax));

        // ── Daily, aligned with the matching day of the previous period ──
        var curByDay = curOk.GroupBy(o => o.Day).ToDictionary(g => g.Key, g => (Rev: g.Sum(o => o.Total), Count: g.Count()));
        var prevByDay = prevOk.GroupBy(o => o.Day).ToDictionary(g => g.Key, g => (Rev: g.Sum(o => o.Total), Count: g.Count()));
        for (var i = 0; i < days; i++)
        {
            var d = from.AddDays(i);
            var p = prevFrom.AddDays(i);
            curByDay.TryGetValue(d, out var c);
            prevByDay.TryGetValue(p, out var pv);
            r.Daily.Add(new DailyPoint { Date = d, Revenue = c.Rev, Orders = c.Count, PrevDate = p, PrevRevenue = pv.Rev, PrevOrders = pv.Count });
        }

        // ── When are we busy? ──
        static int Weekday(DateTime local) => ((int)local.DayOfWeek + 6) % 7; // Monday = 0
        var cells = curOk.GroupBy(o => (Weekday(o.Local), o.Local.Hour))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Rev: g.Sum(o => o.Total)));
        for (var wd = 0; wd < 7; wd++)
            for (var h = 0; h < 24; h++)
            {
                cells.TryGetValue((wd, h), out var c);
                r.Heatmap.Add(new HeatCell { Weekday = wd, Hour = h, Orders = c.Count, Revenue = c.Rev });
            }
        r.Hourly = Enumerable.Range(0, 24).Select(h => new HourPoint
        {
            Hour = h,
            Orders = curOk.Count(o => o.Local.Hour == h),
            Revenue = curOk.Where(o => o.Local.Hour == h).Sum(o => o.Total)
        }).ToList();

        // ── Menu: categories, top items, movers, items not selling ──
        var curLines = curOk.SelectMany(o => o.Items).ToList();
        var prevLines = prevOk.SelectMany(o => o.Items).ToList();
        var itemRevenueTotal = curLines.Sum(i => i.Revenue);

        var byCategory = curLines
            .GroupBy(i => categoryOf.GetValueOrDefault(i.MenuItemId) ?? "Uncategorised")
            .Select(g => new CategoryShare { Name = g.Key, Revenue = g.Sum(i => i.Revenue), Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(c => c.Revenue).ToList();
        if (byCategory.Count > 8)
        {
            var tail = byCategory.Skip(7).ToList();
            byCategory = byCategory.Take(7).Append(new CategoryShare { Name = $"Other ({tail.Count})", Revenue = tail.Sum(c => c.Revenue), Quantity = tail.Sum(c => c.Quantity) }).ToList();
        }
        foreach (var c in byCategory) c.SharePct = Math.Round(Pct(c.Revenue, itemRevenueTotal), 1);
        r.Categories = byCategory;

        var prevQty = prevLines.GroupBy(i => i.MenuItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var itemPerf = curLines.GroupBy(i => i.MenuItemId).Select(g =>
        {
            var qty = g.Sum(i => i.Quantity);
            var pq = prevQty.GetValueOrDefault(g.Key);
            return new ItemPerformance
            {
                MenuItemId = g.Key,
                Name = g.First().Name,
                Category = categoryOf.GetValueOrDefault(g.Key),
                Quantity = qty,
                Revenue = g.Sum(i => i.Revenue),
                SharePct = Math.Round(Pct(g.Sum(i => i.Revenue), itemRevenueTotal), 1),
                PrevQuantity = pq,
                ChangePct = pq == 0 ? null : Math.Round((decimal)(qty - pq) / pq * 100, 0)
            };
        }).ToList();
        r.TopItems = itemPerf.OrderByDescending(i => i.Revenue).Take(10).ToList();

        // Movers need enough volume to mean something
        var movers = itemPerf.Where(i => i.Quantity + i.PrevQuantity >= 6 && i.PrevQuantity > 0).ToList();
        r.Rising = movers.Where(i => i.ChangePct > 0).OrderByDescending(i => i.ChangePct).Take(3).ToList();
        var dropped = prevLines.GroupBy(i => i.MenuItemId)
            .Where(g => !itemPerf.Any(c => c.MenuItemId == g.Key) && g.Sum(i => i.Quantity) >= 3)
            .Select(g => new ItemPerformance
            {
                MenuItemId = g.Key, Name = g.First().Name, Category = categoryOf.GetValueOrDefault(g.Key),
                Quantity = 0, PrevQuantity = g.Sum(i => i.Quantity), ChangePct = -100
            });
        r.Falling = movers.Where(i => i.ChangePct < 0).Concat(dropped).OrderBy(i => i.ChangePct).ThenByDescending(i => i.PrevQuantity).Take(3).ToList();

        var soldIds = curLines.Select(i => i.MenuItemId).ToHashSet();
        var rangeEndUtc = BusinessClock.StartOfDayUtc(to.AddDays(1));
        var notSelling = menu.Where(m => m.IsAvailable && m.CreatedAt < rangeEndUtc && !soldIds.Contains(m.Id))
            .OrderBy(m => m.Category).ThenBy(m => m.Name).ToList();
        r.NotSellingCount = notSelling.Count;
        r.NotSelling = notSelling.Take(12).Select(m => m.Name).ToList();

        // ── Kitchen speed (median: one forgotten order shouldn't wreck the number) ──
        static decimal Median(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            xs.Sort();
            var mid = xs.Count / 2;
            return (decimal)Math.Round(xs.Count % 2 == 1 ? xs[mid] : (xs[mid - 1] + xs[mid]) / 2, 1);
        }
        static List<double> PrepMinutes(IEnumerable<OrderRow> s) => s
            .Where(o => o.AcceptedAt != null && o.PreparedAt != null && o.PreparedAt > o.AcceptedAt)
            .Select(o => (o.PreparedAt!.Value - o.AcceptedAt!.Value).TotalMinutes).Where(m => m < 240).ToList();
        static List<double> ServeMinutes(IEnumerable<OrderRow> s) => s
            .Where(o => o.ServedAt != null && o.ServedAt > o.CreatedAt)
            .Select(o => (o.ServedAt!.Value - o.CreatedAt).TotalMinutes).Where(m => m < 240).ToList();
        static (decimal Pct, int N) OnTime(IEnumerable<OrderRow> s)
        {
            var measured = s.Where(o => o.PreparedAt != null && o.PreparedAt > o.CreatedAt && o.EstimatedMinutes > 0).ToList();
            if (measured.Count == 0) return (0, 0);
            var ok = measured.Count(o => (o.PreparedAt!.Value - o.CreatedAt).TotalMinutes <= o.EstimatedMinutes);
            return (Math.Round((decimal)ok / measured.Count * 100, 1), measured.Count);
        }
        var onTime = OnTime(curOk);
        r.Kitchen = new KitchenPerformance
        {
            MedianPrepMinutes = Metric.Of(Median(PrepMinutes(curOk)), Median(PrepMinutes(prevOk))),
            MedianServeMinutes = Metric.Of(Median(ServeMinutes(curOk)), Median(ServeMinutes(prevOk))),
            OnTimePct = Metric.Of(onTime.Pct, OnTime(prevOk).Pct),
            Measured = onTime.N
        };

        // ── Where orders come from, and which tables earn most ──
        r.Sources = new OrderSources
        {
            QrOrders = curOk.Count(o => o.ViaQr), QrRevenue = curOk.Where(o => o.ViaQr).Sum(o => o.Total),
            StaffOrders = curOk.Count(o => !o.ViaQr), StaffRevenue = curOk.Where(o => !o.ViaQr).Sum(o => o.Total)
        };
        r.Tables = curOk.GroupBy(o => o.Table).Select(g => new TablePerformance
        {
            Table = string.IsNullOrEmpty(g.Key) ? "—" : g.Key,
            Orders = g.Count(),
            Revenue = g.Sum(o => o.Total),
            AvgOrderValue = Math.Round(g.Average(o => o.Total), 2)
        }).OrderByDescending(t => t.Revenue).Take(10).ToList();

        r.DuesAging = await DuesAgingAsync();
        r.Insights = BuildInsights(r, days);
        return r;
    }

    private async Task<List<DueBucket>> DuesAgingAsync()
    {
        var dues = await _db.CustomerDues.Where(d => !d.IsSettled).Select(d => new { d.CreatedAt, d.DueAmount }).ToListAsync();
        var today = BusinessClock.Today;
        int Age(DateTime createdUtc) => today.DayNumber - DateOnly.FromDateTime(BusinessClock.ToLocal(createdUtc)).DayNumber;
        (string Label, int Min, int Max)[] buckets = { ("0–7 days", 0, 7), ("8–30 days", 8, 30), ("31–90 days", 31, 90), ("Over 90 days", 91, int.MaxValue) };
        return buckets.Select(b =>
        {
            var inBucket = dues.Where(d => Age(d.CreatedAt) >= b.Min && Age(d.CreatedAt) <= b.Max).ToList();
            return new DueBucket { Label = b.Label, Count = inBucket.Count, Amount = inBucket.Sum(d => d.DueAmount) };
        }).ToList();
    }

    private static List<Insight> BuildInsights(AnalyticsOverviewResponse r, int days)
    {
        var list = new List<Insight>();
        var period = days == 1 ? "the previous day" : $"the previous {days} days";
        if (r.Orders.Value == 0)
        {
            list.Add(new Insight { Icon = "📭", Text = "No orders in this period yet." });
            return list;
        }

        if (r.Revenue.ChangePct is { } rc && Math.Abs(rc) >= 3)
            list.Add(new Insight
            {
                Icon = rc > 0 ? "📈" : "📉", Tone = rc > 0 ? "good" : "bad",
                Text = $"Revenue is {(rc > 0 ? "up" : "down")} {Math.Abs(rc):0.#}% vs {period} ({Rs(r.Revenue.Previous)} → {Rs(r.Revenue.Value)})."
            });

        var busiest = r.Heatmap.OrderByDescending(c => c.Orders).First();
        if (busiest.Orders > 0)
            list.Add(new Insight { Icon = "⏰", Text = $"Busiest time: {WeekdayNames[busiest.Weekday]}s {HourRange(busiest.Hour)} ({busiest.Orders} orders). Plan staff and prep around it." });

        if (days >= 7)
        {
            var byWeekday = r.Daily.GroupBy(d => ((int)d.Date.DayOfWeek + 6) % 7)
                .Select(g => (Weekday: g.Key, Avg: g.Average(d => d.Revenue))).OrderByDescending(x => x.Avg).ToList();
            if (byWeekday.Count >= 2 && byWeekday[0].Avg > 0)
                list.Add(new Insight { Icon = "📅", Text = $"{WeekdayNames[byWeekday[0].Weekday]} is your best day (avg {Rs(byWeekday[0].Avg)}); {WeekdayNames[byWeekday[^1].Weekday]} is the quietest (avg {Rs(byWeekday[^1].Avg)})." });
        }

        if (r.TopItems.FirstOrDefault() is { } top)
            list.Add(new Insight { Icon = "⭐", Text = $"{top.Name} is your top earner: {top.SharePct:0.#}% of item sales ({top.Quantity} sold)." });

        if (r.Rising.FirstOrDefault() is { } up)
            list.Add(new Insight { Icon = "🚀", Tone = "good", Text = $"{up.Name} is trending up: {up.Quantity} sold vs {up.PrevQuantity} ({up.ChangePct:+0;-0}%)." });

        if (r.DiscountRate.Value >= 5)
            list.Add(new Insight { Icon = "💸", Tone = "bad", Text = $"Discounts were {r.DiscountRate.Value:0.#}% of sales ({Rs(r.Discounts.Value)}). Check they're bringing customers back." });

        if (r.CancelRate.Value >= 5)
            list.Add(new Insight { Icon = "⚠️", Tone = "bad", Text = $"{r.CancelRate.Value:0.#}% of orders were cancelled ({r.CancelledOrders.Value:0}). Look for items that run out or take too long." });

        if (r.Kitchen.Measured >= 10 && r.Kitchen.OnTimePct.Value < 80)
            list.Add(new Insight { Icon = "🍳", Tone = "bad", Text = $"Only {r.Kitchen.OnTimePct.Value:0}% of orders were ready within their estimated time (median prep {r.Kitchen.MedianPrepMinutes.Value:0.#} min)." });

        if (r.NotSellingCount > 0)
            list.Add(new Insight { Icon = "🧊", Text = $"{r.NotSellingCount} menu item{(r.NotSellingCount == 1 ? "" : "s")} didn't sell at all — consider promoting, re-pricing or removing {(r.NotSellingCount == 1 ? "it" : "them")}." });

        var totalOrders = r.Sources.QrOrders + r.Sources.StaffOrders;
        if (totalOrders > 0 && r.Sources.QrOrders > 0)
            list.Add(new Insight { Icon = "📱", Text = $"{(decimal)r.Sources.QrOrders / totalOrders * 100:0}% of orders were placed by customers scanning the table QR code." });

        var oldDues = r.DuesAging.Where(b => b.Label is "31–90 days" or "Over 90 days").Sum(b => b.Amount);
        if (oldDues > 0)
            list.Add(new Insight { Icon = "📒", Tone = "bad", Text = $"{Rs(oldDues)} of udhaar is older than a month. Follow up before it becomes hard to collect." });

        return list;
    }

    private static string HourRange(int h)
    {
        static string Fmt(int x) => x == 0 || x == 24 ? "12 AM" : x == 12 ? "12 PM" : x < 12 ? $"{x} AM" : $"{x - 12} PM";
        return $"{Fmt(h).Replace(" ", " ")}–{Fmt(h + 1).Replace(" ", " ")}";
    }

    // ═══════════════════════════════════════════════════
    // Today dashboard
    // ═══════════════════════════════════════════════════

    public async Task<TodayDashboardResponse> GetTodayAsync()
    {
        var now = BusinessClock.Now;
        var today = DateOnly.FromDateTime(now);
        var compareDay = today.AddDays(-7);
        var sinceMidnight = now - now.Date;

        // Last 8 local days covers today, the comparison day and the 7-day trend
        var (startUtc, endUtc) = BusinessClock.DayRangeUtc(today.AddDays(-7).ToDateTime(TimeOnly.MinValue), today.ToDateTime(TimeOnly.MinValue));
        var orders = await LoadOrdersAsync(startUtc, endUtc);
        var ok = orders.Where(o => o.Counts).ToList();

        var todayOk = ok.Where(o => o.Day == today).ToList();
        var compareOk = ok.Where(o => o.Day == compareDay).ToList();
        var compareSoFar = compareOk.Where(o => o.Local - o.Local.Date <= sinceMidnight).ToList();

        decimal Avg(List<OrderRow> s) => s.Count == 0 ? 0 : s.Average(o => o.Total);
        var r = new TodayDashboardResponse
        {
            Now = DateTime.UtcNow, // serialized as UTC; the browser shows it in local time
            Date = today,
            CompareDate = compareDay,
            Revenue = Metric.Of(todayOk.Sum(o => o.Total), compareSoFar.Sum(o => o.Total)),
            Orders = Metric.Of(todayOk.Count, compareSoFar.Count),
            AvgOrderValue = Metric.Of(Avg(todayOk), Avg(compareSoFar)),
            Discounts = Metric.Of(todayOk.Sum(o => o.Discount), compareSoFar.Sum(o => o.Discount))
        };

        decimal todayRun = 0, weekRun = 0;
        for (var h = 0; h < 24; h++)
        {
            todayRun += todayOk.Where(o => o.Local.Hour == h).Sum(o => o.Total);
            weekRun += compareOk.Where(o => o.Local.Hour == h).Sum(o => o.Total);
            r.Cumulative.Add(new CumulativePoint { Hour = h, Today = h <= now.Hour ? todayRun : null, LastWeek = weekRun });
        }

        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var dayOrders = ok.Where(o => o.Day == d).ToList();
            r.Last7Days.Add(new DailyPoint { Date = d, Revenue = dayOrders.Sum(o => o.Total), Orders = dayOrders.Count });
        }
        r.Last7Total = r.Last7Days.Sum(d => d.Revenue);

        // ── Right now (all open orders, whatever day they started) ──
        var openStatuses = new[] { OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served };
        var open = await _db.Orders.Where(o => openStatuses.Contains(o.Status))
            .Select(o => new { o.Status, o.TableId, o.CreatedAt }).ToListAsync();
        var waiting = open.Where(o => o.Status is OrderStatus.Pending or OrderStatus.Accepted or OrderStatus.Preparing).ToList();
        r.Live = new LiveStatus
        {
            Pending = open.Count(o => o.Status is OrderStatus.Pending or OrderStatus.Accepted),
            Preparing = open.Count(o => o.Status == OrderStatus.Preparing),
            Ready = open.Count(o => o.Status == OrderStatus.Ready),
            Served = open.Count(o => o.Status == OrderStatus.Served),
            OccupiedTables = open.Select(o => o.TableId).Distinct().Count(),
            TotalTables = await _db.Tables.CountAsync(),
            OldestWaitMinutes = waiting.Count == 0 ? 0 : (int)(DateTime.UtcNow - waiting.Min(o => o.CreatedAt)).TotalMinutes
        };

        var lines = todayOk.SelectMany(o => o.Items).ToList();
        var lineTotal = lines.Sum(i => i.Revenue);
        r.TopItems = lines.GroupBy(i => i.MenuItemId).Select(g => new ItemPerformance
        {
            MenuItemId = g.Key, Name = g.First().Name, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Revenue),
            SharePct = lineTotal == 0 ? 0 : Math.Round(g.Sum(i => i.Revenue) / lineTotal * 100, 1)
        }).OrderByDescending(i => i.Revenue).Take(5).ToList();

        r.RecentOrders = orders.Where(o => o.Day == today).OrderByDescending(o => o.CreatedAt).Take(8)
            .Select(o => new RecentOrder
            {
                Id = o.Id, OrderNumber = o.OrderNumber, Table = o.Table, Status = o.Status.ToString(),
                Total = o.Total, Items = o.Items.Sum(i => i.Quantity), CreatedAt = o.CreatedAt
            }).ToList();

        // ── Things that need attention ──
        var stock = await _db.InventoryItems.Where(i => i.IsActive)
            .Select(i => new { i.CurrentQuantity, i.MinimumQuantity }).ToListAsync();
        r.OutOfStockCount = stock.Count(i => i.CurrentQuantity <= 0 && i.MinimumQuantity > 0);
        r.LowStockCount = stock.Count(i => i.CurrentQuantity > 0 && i.MinimumQuantity > 0 && i.CurrentQuantity <= i.MinimumQuantity);
        var dues = await _db.CustomerDues.Where(d => !d.IsSettled).Select(d => new { d.DueAmount, d.CreatedAt }).ToListAsync();
        r.UnsettledDues = dues.Sum(d => d.DueAmount);
        r.UnsettledDueCount = dues.Count;
        r.OldestDueDays = dues.Count == 0 ? 0 : today.DayNumber - DateOnly.FromDateTime(BusinessClock.ToLocal(dues.Min(d => d.CreatedAt))).DayNumber;

        // ── Plain-language read of the day ──
        var weekdayName = WeekdayNames[((int)compareDay.DayOfWeek + 6) % 7];
        var compareFull = compareOk.Sum(o => o.Total);
        var compareSoFarRev = compareSoFar.Sum(o => o.Total);
        if (r.Revenue.ChangePct is { } pct && compareSoFarRev > 0)
        {
            r.Insights.Add(new Insight
            {
                Icon = pct >= 0 ? "📈" : "📉", Tone = pct >= 0 ? "good" : "bad",
                Text = $"{Math.Abs(pct):0}% {(pct >= 0 ? "ahead of" : "behind")} last {weekdayName} at this time ({Rs(compareSoFarRev)} by {now:h:mm tt})."
            });
            // Additive projection: today so far + what last week did for the rest of the day
            // (a ratio would turn a strong lunch into an unrealistic day total)
            var projected = r.Revenue.Value + (compareFull - compareSoFarRev);
            if (now.Hour < 22 && compareFull > compareSoFarRev)
                r.Insights.Add(new Insight { Icon = "🎯", Text = $"At this pace you'll end the day around {Rs(Math.Round(projected / 100) * 100)} (last {weekdayName}: {Rs(compareFull)})." });
        }
        var upcoming = compareOk.Where(o => o.Local.Hour > now.Hour).GroupBy(o => o.Local.Hour)
            .OrderByDescending(g => g.Count()).FirstOrDefault();
        if (upcoming != null && upcoming.Count() >= 3)
            r.Insights.Add(new Insight { Icon = "⏰", Text = $"Rush ahead: last {weekdayName} {HourRange(upcoming.Key)} brought {upcoming.Count()} orders." });
        if (r.Live.OldestWaitMinutes >= 30)
            r.Insights.Add(new Insight { Icon = "⏳", Tone = "bad", Text = $"An order has been waiting {r.Live.OldestWaitMinutes} minutes. Check the kitchen." });
        if (r.OutOfStockCount + r.LowStockCount > 0)
            r.Insights.Add(new Insight { Icon = "📦", Tone = "bad", Text = $"{r.OutOfStockCount} item{(r.OutOfStockCount == 1 ? "" : "s")} out of stock and {r.LowStockCount} running low." });

        return r;
    }
}
