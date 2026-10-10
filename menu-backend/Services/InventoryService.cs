using menu_backend.Data;
using menu_backend.DTOs.Inventory;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using menu_backend.Helpers;
using menu_backend.DTOs.Analytics;

namespace menu_backend.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<InventoryItemResponse>> GetItemsAsync(string? category = null, bool? lowStockOnly = null)
    {
        var query = _db.InventoryItems.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(i => i.Category == category);

        if (lowStockOnly == true)
            query = query.Where(i => i.CurrentQuantity <= i.MinimumQuantity && i.MinimumQuantity > 0);

        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .Select(i => MapItem(i))
            .ToListAsync();
    }

    public async Task<InventoryItemResponse?> GetItemAsync(Guid id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        return item == null ? null : MapItem(item);
    }

    public async Task<InventoryItemResponse> CreateItemAsync(CreateInventoryItemRequest request)
    {
        if (!Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            throw new ArgumentException($"Invalid unit: {request.Unit}");

        var item = new InventoryItem
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            CurrentQuantity = request.CurrentQuantity,
            Unit = unit,
            MinimumQuantity = request.MinimumQuantity,
            CostPerUnit = request.CostPerUnit,
            Supplier = request.Supplier,
            SupplierContact = request.SupplierContact,
            IsActive = true,
            LastRestockedAt = request.CurrentQuantity > 0 ? DateTime.UtcNow : null
        };

        _db.InventoryItems.Add(item);

        // Log initial stock
        if (request.CurrentQuantity > 0)
        {
            _db.InventoryLogs.Add(new InventoryLog
            {
                TenantId = item.TenantId,
                InventoryItemId = item.Id,
                QuantityChange = request.CurrentQuantity,
                QuantityAfter = request.CurrentQuantity,
                ChangeType = "Restock",
                Notes = "Initial stock",
                ChangedBy = "System"
            });
        }

        await _db.SaveChangesAsync();
        return MapItem(item);
    }

    public async Task<InventoryItemResponse> UpdateItemAsync(Guid id, UpdateInventoryItemRequest request)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = request.Description;
        if (request.Category != null) item.Category = request.Category;
        if (request.Unit != null && Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            item.Unit = unit;
        if (request.MinimumQuantity.HasValue) item.MinimumQuantity = request.MinimumQuantity.Value;
        if (request.CostPerUnit.HasValue) item.CostPerUnit = request.CostPerUnit.Value;
        if (request.Supplier != null) item.Supplier = request.Supplier;
        if (request.SupplierContact != null) item.SupplierContact = request.SupplierContact;
        if (request.IsActive.HasValue) item.IsActive = request.IsActive.Value;

        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapItem(item);
    }

    public async Task DeleteItemAsync(Guid id)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<InventoryItemResponse> AdjustQuantityAsync(Guid id, AdjustQuantityRequest request, string? userName)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        var validTypes = new[] { "Restock", "Usage", "Wastage", "Adjustment" };
        if (!validTypes.Contains(request.ChangeType))
            throw new ArgumentException($"Invalid change type: {request.ChangeType}");

        decimal change;
        if (request.ChangeType == "Restock" || request.ChangeType == "Adjustment")
        {
            change = Math.Abs(request.Quantity);
        }
        else
        {
            change = -Math.Abs(request.Quantity);
        }

        item.CurrentQuantity += change;
        if (item.CurrentQuantity < 0) item.CurrentQuantity = 0;

        if (request.ChangeType == "Restock")
            item.LastRestockedAt = DateTime.UtcNow;

        item.UpdatedAt = DateTime.UtcNow;

        _db.InventoryLogs.Add(new InventoryLog
        {
            TenantId = item.TenantId,
            InventoryItemId = item.Id,
            QuantityChange = change,
            QuantityAfter = item.CurrentQuantity,
            ChangeType = request.ChangeType,
            Notes = request.Notes,
            ChangedBy = userName
        });

        await _db.SaveChangesAsync();
        return MapItem(item);
    }

    public async Task<List<InventoryLogResponse>> GetLogsAsync(Guid? itemId = null, int days = 30)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        var query = _db.InventoryLogs
            .Include(l => l.InventoryItem)
            .Where(l => l.CreatedAt >= fromDate);

        if (itemId.HasValue)
            query = query.Where(l => l.InventoryItemId == itemId.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .Select(l => new InventoryLogResponse
            {
                Id = l.Id,
                InventoryItemId = l.InventoryItemId,
                ItemName = l.InventoryItem != null ? l.InventoryItem.Name : "",
                QuantityChange = l.QuantityChange,
                QuantityAfter = l.QuantityAfter,
                ChangeType = l.ChangeType,
                Notes = l.Notes,
                ChangedBy = l.ChangedBy,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync()
    {
        var items = await _db.InventoryItems.Where(i => i.IsActive).ToListAsync();

        // Low = running low but not empty; out = empty. Kept separate so no item is counted twice.
        var lowStock = items.Where(i => i.MinimumQuantity > 0 && i.CurrentQuantity > 0 && i.CurrentQuantity <= i.MinimumQuantity).ToList();
        var outOfStock = items.Where(i => i.CurrentQuantity <= 0).ToList();

        var categoryBreakdown = items
            .GroupBy(i => i.Category ?? "Uncategorized")
            .Select(g => new CategorySummary
            {
                Category = g.Key,
                ItemCount = g.Count(),
                TotalValue = g.Sum(i => i.CurrentQuantity * i.CostPerUnit)
            })
            .OrderByDescending(c => c.TotalValue)
            .ToList();

        return new InventorySummaryResponse
        {
            TotalItems = items.Count,
            LowStockItems = lowStock.Count,
            OutOfStockItems = outOfStock.Count,
            TotalInventoryValue = items.Sum(i => i.CurrentQuantity * i.CostPerUnit),
            // Alerts cover both: anything that needs restocking, emptiest first
            LowStockAlerts = outOfStock.Concat(lowStock).Select(MapItem).ToList(),
            CategoryBreakdown = categoryBreakdown
        };
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _db.InventoryItems
            .Where(i => i.Category != null && i.Category != "")
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    private static InventoryItemResponse MapItem(InventoryItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Category = item.Category,
        CurrentQuantity = item.CurrentQuantity,
        Unit = item.Unit.ToString(),
        MinimumQuantity = item.MinimumQuantity,
        CostPerUnit = item.CostPerUnit,
        Supplier = item.Supplier,
        SupplierContact = item.SupplierContact,
        LastRestockedAt = item.LastRestockedAt,
        IsActive = item.IsActive,
        IsLowStock = item.MinimumQuantity > 0 && item.CurrentQuantity <= item.MinimumQuantity,
        TotalValue = item.CurrentQuantity * item.CostPerUnit,
        CreatedAt = item.CreatedAt
    };

    public async Task DeductForOrderAsync(Guid orderId, string? changedBy = null)
    {
        // Load order items
        var orderItems = await _db.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .ToListAsync();

        if (orderItems.Count == 0) return;

        // Load all ingredient mappings for the menu items in this order
        var menuItemIds = orderItems.Select(oi => oi.MenuItemId).Distinct().ToArray();
        var ingredients = new List<MenuItemIngredient>();
        foreach (var menuItemId in menuItemIds)
        {
            var itemIngredients = await _db.MenuItemIngredients
                .Where(i => i.MenuItemId == menuItemId)
                .Include(i => i.InventoryItem)
                .ToListAsync();
            ingredients.AddRange(itemIngredients);
        }

        if (ingredients.Count == 0) return;

        // Aggregate deductions per inventory item
        var deductions = new Dictionary<Guid, decimal>();
        foreach (var oi in orderItems)
        {
            var itemIngredients = ingredients.Where(i => i.MenuItemId == oi.MenuItemId);
            foreach (var ing in itemIngredients)
            {
                var totalUsage = ing.QuantityUsed * oi.Quantity;
                if (deductions.ContainsKey(ing.InventoryItemId))
                    deductions[ing.InventoryItemId] += totalUsage;
                else
                    deductions[ing.InventoryItemId] = totalUsage;
            }
        }

        // Apply deductions and create logs
        foreach (var (inventoryItemId, totalDeduction) in deductions)
        {
            var invItem = await _db.InventoryItems.FindAsync(inventoryItemId);
            if (invItem == null) continue;

            invItem.CurrentQuantity = Math.Max(0, invItem.CurrentQuantity - totalDeduction);
            invItem.UpdatedAt = DateTime.UtcNow;

            _db.InventoryLogs.Add(new Models.InventoryLog
            {
                InventoryItemId = inventoryItemId,
                QuantityChange = -totalDeduction,
                QuantityAfter = invItem.CurrentQuantity,
                ChangeType = "Usage",
                Notes = $"Auto-deducted for order",
                ChangedBy = changedBy ?? "System"
            });
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════
    // Analytics: stock health now + usage, waste and restocks over the last N days
    // ═══════════════════════════════════════════════════

    public async Task<InventoryAnalyticsResponse> GetAnalyticsAsync(int days)
    {
        days = Math.Clamp(days, 1, 365);
        var to = BusinessClock.Today;
        var from = to.AddDays(-(days - 1));
        var prevFrom = from.AddDays(-days);
        var (prevStartUtc, _) = BusinessClock.DayRangeUtc(prevFrom);
        var startUtc = BusinessClock.StartOfDayUtc(from);

        var items = await _db.InventoryItems.Where(i => i.IsActive).ToListAsync();
        var byId = items.ToDictionary(i => i.Id);
        var logs = await _db.InventoryLogs
            .Where(l => l.CreatedAt >= prevStartUtc)
            .Select(l => new { l.InventoryItemId, l.QuantityChange, l.ChangeType, l.ChangedBy, l.Notes, l.CreatedAt })
            .ToListAsync();
        logs = logs.Where(l => byId.ContainsKey(l.InventoryItemId)).ToList();

        decimal Cost(Guid id) => byId[id].CostPerUnit;
        string UnitOf(Guid id) => byId[id].Unit.ToString().ToLowerInvariant() switch { "piece" => "pcs", "liter" => "L", "gram" => "g", var u => u };
        var cur = logs.Where(l => l.CreatedAt >= startUtc).ToList();
        var prev = logs.Where(l => l.CreatedAt < startUtc).ToList();
        var curUse = cur.Where(l => l.ChangeType == "Usage").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();
        var curWaste = cur.Where(l => l.ChangeType == "Wastage").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();
        var curRestock = cur.Where(l => l.ChangeType == "Restock").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();
        var prevUse = prev.Where(l => l.ChangeType == "Usage").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();
        var prevWaste = prev.Where(l => l.ChangeType == "Wastage").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();
        var prevRestock = prev.Where(l => l.ChangeType == "Restock").Select(l => (l.InventoryItemId, Math.Abs(l.QuantityChange))).ToList();

        decimal Val(List<(Guid Id, decimal Qty)> rows) => rows.Sum(r => r.Qty * Cost(r.Id));
        decimal Pct(decimal part, decimal whole) => whole == 0 ? 0 : part / whole * 100;

        var r = new InventoryAnalyticsResponse
        {
            Days = days, From = from, To = to,
            TotalItems = items.Count,
            OutOfStock = items.Count(i => i.CurrentQuantity <= 0),
            LowStock = items.Count(i => i.MinimumQuantity > 0 && i.CurrentQuantity > 0 && i.CurrentQuantity <= i.MinimumQuantity),
            StockValue = Math.Round(items.Sum(i => i.CurrentQuantity * i.CostPerUnit), 2),
            ItemsWithoutCost = items.Count(i => i.CostPerUnit <= 0),
            ItemsWithoutMinimum = items.Count(i => i.MinimumQuantity <= 0),
            UsedValue = Metric.Of(Val(curUse), Val(prevUse)),
            WastedValue = Metric.Of(Val(curWaste), Val(prevWaste)),
            RestockedValue = Metric.Of(Val(curRestock), Val(prevRestock)),
            WasteRate = Metric.Of(Pct(Val(curWaste), Val(curUse) + Val(curWaste)), Pct(Val(prevWaste), Val(prevUse) + Val(prevWaste))),
            AutoDeductions = cur.Count(l => l.ChangeType == "Usage" && l.Notes != null && l.Notes.StartsWith("Auto-deducted"))
        };
        r.InStock = r.TotalItems - r.LowStock - r.OutOfStock;

        // ── Daily usage / waste / restock value ──
        var curByDay = cur.GroupBy(l => DateOnly.FromDateTime(BusinessClock.ToLocal(l.CreatedAt))).ToDictionary(g => g.Key, g => g.ToList());
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            curByDay.TryGetValue(d, out var dayLogs);
            dayLogs ??= new();
            decimal V(string type) => dayLogs.Where(l => l.ChangeType == type).Sum(l => Math.Abs(l.QuantityChange) * Cost(l.InventoryItemId));
            r.Daily.Add(new InventoryDailyPoint { Date = d, UsedValue = Math.Round(V("Usage"), 2), WastedValue = Math.Round(V("Wastage"), 2), RestockedValue = Math.Round(V("Restock"), 2) });
        }

        // ── How long will stock last? ──
        var usedQty = curUse.GroupBy(u => u.Item1).ToDictionary(g => g.Key, g => g.Sum(u => u.Item2));
        foreach (var item in items)
        {
            if (!usedQty.TryGetValue(item.Id, out var used) || used <= 0) continue;
            // Items added during the period: average over the days they've existed
            var activeDays = Math.Max(1, Math.Min(days, to.DayNumber - DateOnly.FromDateTime(BusinessClock.ToLocal(item.CreatedAt)).DayNumber + 1));
            var perDay = used / activeDays;
            var daysLeft = perDay > 0 ? item.CurrentQuantity / perDay : 999;
            if (daysLeft > 7) continue; // the urgent ones: out within a week
            var cover = perDay * 14 - item.CurrentQuantity;
            r.RunningOut.Add(new StockRunway
            {
                Id = item.Id, Name = item.Name, Unit = UnitOf(item.Id), CurrentQuantity = Math.Round(item.CurrentQuantity, 2),
                AvgDailyUse = Math.Round(perDay, 2), DaysLeft = Math.Round(daysLeft, 1), Supplier = item.Supplier,
                SuggestedOrder = cover <= 0 ? 0 : item.Unit is InventoryUnit.Piece or InventoryUnit.Dozen or InventoryUnit.Box or InventoryUnit.Packet or InventoryUnit.Bottle or InventoryUnit.Can or InventoryUnit.Bunch
                    ? Math.Ceiling(cover) : Math.Ceiling(cover * 2) / 2
            });
        }
        r.RunningOut = r.RunningOut.OrderBy(x => x.DaysLeft).Take(10).ToList();

        // ── Where the money goes / sits ──
        List<InventoryItemMovement> Rank(List<(Guid Id, decimal Qty)> rows, int take)
        {
            var total = Val(rows);
            return rows.GroupBy(x => x.Id).Select(g => new InventoryItemMovement
            {
                Id = g.Key, Name = byId[g.Key].Name, Category = byId[g.Key].Category, Unit = UnitOf(g.Key),
                Quantity = Math.Round(g.Sum(x => x.Qty), 2), Value = Math.Round(g.Sum(x => x.Qty) * Cost(g.Key), 2),
                SharePct = Math.Round(Pct(g.Sum(x => x.Qty) * Cost(g.Key), total), 1)
            }).OrderByDescending(x => x.Value).ThenByDescending(x => x.Quantity).Take(take).ToList();
        }
        r.TopUsed = Rank(curUse, 8);
        r.TopWasted = Rank(curWaste, 6);
        r.TopValue = items.Where(i => i.CurrentQuantity > 0).Select(i => new InventoryItemMovement
        {
            Id = i.Id, Name = i.Name, Category = i.Category, Unit = UnitOf(i.Id), Quantity = Math.Round(i.CurrentQuantity, 2),
            Value = Math.Round(i.CurrentQuantity * i.CostPerUnit, 2), SharePct = Math.Round(Pct(i.CurrentQuantity * i.CostPerUnit, r.StockValue), 1)
        }).OrderByDescending(x => x.Value).Take(8).ToList();

        var moved = cur.Where(l => l.ChangeType is "Usage" or "Wastage").Select(l => l.InventoryItemId).ToHashSet();
        var dead = items.Where(i => i.CurrentQuantity > 0 && !moved.Contains(i.Id) && i.CreatedAt < startUtc).ToList();
        r.DeadStockValue = Math.Round(dead.Sum(i => i.CurrentQuantity * i.CostPerUnit), 2);
        r.DeadStock = dead.Select(i => new InventoryItemMovement
        {
            Id = i.Id, Name = i.Name, Category = i.Category, Unit = UnitOf(i.Id), Quantity = Math.Round(i.CurrentQuantity, 2),
            Value = Math.Round(i.CurrentQuantity * i.CostPerUnit, 2)
        }).OrderByDescending(x => x.Value).Take(8).ToList();

        r.ValueByCategory = items.GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "Uncategorised" : i.Category!)
            .Select(g => new CategorySummary { Category = g.Key, ItemCount = g.Count(), TotalValue = Math.Round(g.Sum(i => i.CurrentQuantity * i.CostPerUnit), 2) })
            .OrderByDescending(c => c.TotalValue).ToList();

        // ── Plain-language findings ──
        var india = new System.Globalization.CultureInfo("en-IN");
        string Rs(decimal v) => "₹" + v.ToString("N0", india);
        string Qty(decimal q, string unit) => $"{q.ToString("0.#", india)} {unit}";
        var period = $"the previous {days} days";

        if (r.RunningOut.FirstOrDefault() is { } soonest)
            r.Insights.Add(new Insight
            {
                Icon = "⏳", Tone = "bad",
                Text = soonest.CurrentQuantity <= 0
                    ? $"{soonest.Name} is out of stock and you use about {Qty(soonest.AvgDailyUse, soonest.Unit)} a day. Order ~{Qty(soonest.SuggestedOrder, soonest.Unit)} for two weeks."
                    : $"{soonest.Name} will run out in about {soonest.DaysLeft:0.#} day{(soonest.DaysLeft == 1 ? "" : "s")} (≈{Qty(soonest.AvgDailyUse, soonest.Unit)}/day). Order ~{Qty(soonest.SuggestedOrder, soonest.Unit)} for two weeks."
            });
        if (r.RunningOut.Count > 1)
            r.Insights.Add(new Insight { Icon = "🛒", Text = $"{r.RunningOut.Count} items will run out within a week at your current usage — see the reorder list." });
        if (r.UsedValue.ChangePct is { } uc && Math.Abs(uc) >= 5)
            r.Insights.Add(new Insight { Icon = uc > 0 ? "📈" : "📉", Text = $"You used {Rs(r.UsedValue.Value)} of stock, {Math.Abs(uc):0}% {(uc > 0 ? "more" : "less")} than {period}." });
        if (r.WastedValue.Value > 0 && r.WasteRate.Value >= 3)
            r.Insights.Add(new Insight
            {
                Icon = "🗑️", Tone = "bad",
                Text = $"{Rs(r.WastedValue.Value)} was wasted ({r.WasteRate.Value:0.#}% of what you used){(r.TopWasted.FirstOrDefault() is { } w ? $", mostly {w.Name}" : "")}."
            });
        if (r.DeadStockValue > 0)
            r.Insights.Add(new Insight { Icon = "🧊", Text = $"{Rs(r.DeadStockValue)} is tied up in {dead.Count} item{(dead.Count == 1 ? "" : "s")} that weren't used in the last {days} days{(r.DeadStock.FirstOrDefault() is { } top ? $" (largest: {top.Name}, {Rs(top.Value)})" : "")}." });
        if (r.AutoDeductions == 0 && curUse.Count == 0 && items.Count > 0)
        {
            var linked = await _db.MenuItemIngredients.AnyAsync();
            r.Insights.Add(new Insight
            {
                Icon = "🔗",
                Text = linked
                    ? "No usage was recorded in this period. Usage is logged when orders are completed or when you record it with ⚡ Adjust."
                    : "Stock isn't deducted automatically yet. Link ingredients to menu items (🔗 on the Menu page) and each completed order will update stock."
            });
        }
        if (r.ItemsWithoutCost > 0)
            r.Insights.Add(new Insight { Icon = "💰", Text = $"{r.ItemsWithoutCost} item{(r.ItemsWithoutCost == 1 ? " has" : "s have")} no cost per unit, so {(r.ItemsWithoutCost == 1 ? "its" : "their")} value isn't counted. Add costs for accurate totals." });
        if (r.ItemsWithoutMinimum > 0)
            r.Insights.Add(new Insight { Icon = "🔔", Text = $"{r.ItemsWithoutMinimum} item{(r.ItemsWithoutMinimum == 1 ? " has" : "s have")} no minimum quantity, so you won't get a low-stock alert for {(r.ItemsWithoutMinimum == 1 ? "it" : "them")}." });

        return r;
    }
}
