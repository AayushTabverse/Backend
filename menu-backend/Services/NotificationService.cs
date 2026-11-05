using menu_backend.Data;
using menu_backend.DTOs.Notification;
using menu_backend.Helpers;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class NotificationService : INotificationService
{
    private const int ReminderHour = 9;

    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<OrderHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task RaiseAsync(string tenantId, NotificationDraft draft)
    {
        var existing = draft.DedupKey == null ? null : await _db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted && n.DedupKey == draft.DedupKey && n.ResolvedAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            _db.Notifications.Add(new Notification
            {
                TenantId = tenantId,
                Type = draft.Type,
                Severity = draft.Severity,
                Title = draft.Title,
                Message = draft.Message,
                Link = draft.Link,
                DedupKey = draft.DedupKey
            });
        }
        else
        {
            // Same condition still going on: keep one notification up to date. Once acknowledged it
            // stays quiet unless things got worse.
            if (draft.Severity > existing.Severity)
            {
                existing.AcknowledgedAt = null;
                existing.AcknowledgedBy = null;
            }
            existing.Type = draft.Type;
            existing.Severity = draft.Severity;
            existing.Title = draft.Title;
            existing.Message = draft.Message;
            existing.Link = draft.Link;
        }

        await _db.SaveChangesAsync();
        await BroadcastAsync(tenantId);
    }

    public async Task ResolveAsync(string tenantId, string dedupKey, string resolvedBy)
    {
        var open = await _db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted && n.DedupKey == dedupKey && n.ResolvedAt == null)
            .ToListAsync();
        if (open.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var n in open)
        {
            n.ResolvedAt = now;
            if (n.AcknowledgedAt == null)
            {
                n.AcknowledgedAt = now;
                n.AcknowledgedBy = resolvedBy;
            }
        }
        await _db.SaveChangesAsync();
        await BroadcastAsync(tenantId);
    }

    public async Task<NotificationSummaryResponse> GetSummaryAsync()
    {
        var unacknowledged = _db.Notifications.Where(n => n.AcknowledgedAt == null);
        var alerts = await unacknowledged
            .Where(n => n.Severity >= NotificationSeverity.Warning)
            .OrderByDescending(n => n.Severity)
            .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new NotificationSummaryResponse
        {
            UnacknowledgedCount = await unacknowledged.CountAsync(),
            ActiveAlerts = alerts.Select(Map).ToList()
        };
    }

    public async Task<PaginatedNotificationsResponse> GetNotificationsAsync(string filter, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Notifications.AsQueryable();
        if (filter == "open")
            query = query.Where(n => n.AcknowledgedAt == null);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedNotificationsResponse
        {
            Items = items.Select(Map).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task AcknowledgeAsync(Guid id, string? userName)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Notification not found.");
        if (n.AcknowledgedAt != null) return;

        n.AcknowledgedAt = DateTime.UtcNow;
        n.AcknowledgedBy = userName;
        await _db.SaveChangesAsync();
        await BroadcastAsync(n.TenantId);
    }

    public async Task<int> AcknowledgeAllAsync(string? userName)
    {
        var open = await _db.Notifications.Where(n => n.AcknowledgedAt == null).ToListAsync();
        if (open.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in open)
        {
            n.AcknowledgedAt = now;
            n.AcknowledgedBy = userName;
        }
        await _db.SaveChangesAsync();
        await BroadcastAsync(open[0].TenantId);
        return open.Count;
    }

    public async Task<int> SendDailyStockRemindersAsync()
    {
        if (BusinessClock.Now.Hour < ReminderHour) return 0;

        var dedupKey = $"stock-reminder:{BusinessClock.Today:yyyyMMdd}";
        var alreadySent = await _db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.DedupKey == dedupKey)
            .Select(n => n.TenantId)
            .ToListAsync();

        var needsRestock = await _db.InventoryItems
            .IgnoreQueryFilters()
            .Where(i => !i.IsDeleted && i.IsActive
                        && (i.CurrentQuantity <= 0 || (i.MinimumQuantity > 0 && i.CurrentQuantity <= i.MinimumQuantity))
                        && !alreadySent.Contains(i.TenantId))
            .OrderBy(i => i.CurrentQuantity)
            .ToListAsync();

        var sent = 0;
        foreach (var group in needsRestock.GroupBy(i => i.TenantId))
        {
            // Yesterday's reminder is superseded by today's
            var older = await _db.Notifications
                .IgnoreQueryFilters()
                .Where(n => n.TenantId == group.Key && n.Type == "StockReminder" && n.ResolvedAt == null)
                .ToListAsync();
            foreach (var n in older)
            {
                n.ResolvedAt = DateTime.UtcNow;
                n.AcknowledgedAt ??= DateTime.UtcNow;
                n.AcknowledgedBy ??= "Replaced by today's reminder";
            }

            var items = group.ToList();
            var outCount = items.Count(i => i.CurrentQuantity <= 0);
            var names = string.Join(", ", items.Take(8).Select(i => i.Name));
            if (items.Count > 8) names += $" and {items.Count - 8} more";

            _db.Notifications.Add(new Notification
            {
                TenantId = group.Key,
                Type = "StockReminder",
                Severity = outCount > 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                Title = $"Restock reminder: {items.Count} item{(items.Count == 1 ? "" : "s")} need restocking",
                Message = outCount > 0 ? $"{outCount} out of stock. {names}" : names,
                Link = "/admin/inventory",
                DedupKey = dedupKey
            });
            await _db.SaveChangesAsync();
            await BroadcastAsync(group.Key);
            sent++;
        }
        return sent;
    }

    /// <summary>Payload-free: staff screens fetch the details from the (management-only) API.</summary>
    private Task BroadcastAsync(string tenantId) =>
        _hub.Clients.Group(tenantId).SendAsync("NotificationsChanged");

    private static NotificationResponse Map(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Severity = n.Severity.ToString(),
        Title = n.Title,
        Message = n.Message,
        Link = n.Link,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt,
        AcknowledgedAt = n.AcknowledgedAt,
        AcknowledgedBy = n.AcknowledgedBy,
        ResolvedAt = n.ResolvedAt
    };
}
