using menu_backend.Data;
using menu_backend.DTOs.Notification;
using menu_backend.DTOs.Print;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class PrintService : IPrintService
{
    /// <summary>A KOT that hasn't printed by now is no use to the kitchen; don't print it late.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    /// <summary>A station that claimed a job and went quiet this long probably crashed; others may retry it.</summary>
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(2);

    private readonly AppDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly INotificationService _notifications;

    public PrintService(AppDbContext db, IHubContext<OrderHub> hub, INotificationService notifications)
    {
        _db = db;
        _hub = hub;
        _notifications = notifications;
    }

    public async Task QueueKotAsync(Guid orderId, string tenantId, string trigger)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        if (tenant == null || !tenant.AutoPrintKot || tenant.KotPrintTrigger != trigger) return;

        // Accept → Preparing → Accepted again must not print twice
        var alreadyQueued = await _db.PrintJobs.IgnoreQueryFilters()
            .AnyAsync(p => p.OrderId == orderId && !p.IsDeleted && !p.IsReprint);
        if (alreadyQueued) return;

        await AddJobAsync(orderId, tenantId, isReprint: false);
    }

    public async Task ReprintKotAsync(Guid orderId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");
        await AddJobAsync(order.Id, order.TenantId, isReprint: true);
    }

    private async Task AddJobAsync(Guid orderId, string tenantId, bool isReprint)
    {
        _db.PrintJobs.Add(new PrintJob
        {
            TenantId = tenantId,
            OrderId = orderId,
            Status = PrintJobStatus.Pending,
            IsReprint = isReprint
        });
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(tenantId).SendAsync("PrintJobCreated");
    }

    public async Task<List<KotTicketResponse>> GetPendingAsync()
    {
        var now = DateTime.UtcNow;
        var since = now - MaxAge;
        var staleClaim = now - ClaimTimeout;

        var jobs = await _db.PrintJobs
            .Include(p => p.Order).ThenInclude(o => o!.Items)
            .Include(p => p.Order).ThenInclude(o => o!.Table)
            // Finished or cancelled orders don't need a KOT any more (e.g. a station opened late)
            .Where(p => p.CreatedAt >= since && p.RetryCount < p.MaxRetries
                        && p.Order!.Status != OrderStatus.Completed && p.Order.Status != OrderStatus.Cancelled
                        && (p.Status == PrintJobStatus.Pending
                            || (p.Status == PrintJobStatus.Printing && p.UpdatedAt < staleClaim)))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        var tenantId = jobs.FirstOrDefault()?.TenantId;
        var copies = tenantId == null ? 1
            : await _db.Tenants.Where(t => t.TenantId == tenantId).Select(t => t.KotCopies).FirstOrDefaultAsync();

        return jobs.Where(j => j.Order != null).Select(j => new KotTicketResponse
        {
            JobId = j.Id,
            OrderId = j.OrderId,
            OrderNumber = j.Order!.OrderNumber,
            OrderType = j.Order.Type.ToString(),
            Destination = j.Order.Table?.TableNumber ?? j.Order.TokenNumber ?? "",
            SpecialInstructions = j.Order.SpecialInstructions,
            CreatedAt = j.Order.CreatedAt,
            Copies = j.IsReprint ? 1 : Math.Max(1, copies),
            IsReprint = j.IsReprint,
            Items = j.Order.Items.Where(i => !i.IsDeleted).Select(i => new KotTicketItem
            {
                Name = i.ItemName,
                Quantity = i.Quantity,
                Modifiers = i.Modifiers,
                Notes = i.Notes
            }).ToList()
        }).ToList();
    }

    public async Task<bool> ClaimAsync(Guid printJobId)
    {
        var now = DateTime.UtcNow;
        var staleClaim = now - ClaimTimeout;
        // Single conditional UPDATE so two stations can't both win
        var claimed = await _db.PrintJobs
            .Where(p => p.Id == printJobId
                        && (p.Status == PrintJobStatus.Pending
                            || (p.Status == PrintJobStatus.Printing && p.UpdatedAt < staleClaim)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PrintJobStatus.Printing)
                .SetProperty(p => p.UpdatedAt, now));
        return claimed == 1;
    }

    public async Task MarkPrintedAsync(Guid printJobId)
    {
        var job = await _db.PrintJobs.FindAsync(printJobId)
            ?? throw new KeyNotFoundException("Print job not found.");

        job.Status = PrintJobStatus.Completed;
        job.PrintedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(Guid printJobId, string errorMessage)
    {
        var job = await _db.PrintJobs.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == printJobId)
            ?? throw new KeyNotFoundException("Print job not found.");

        job.RetryCount++;
        job.ErrorMessage = errorMessage.Length > 500 ? errorMessage[..500] : errorMessage;
        job.Status = job.RetryCount >= job.MaxRetries ? PrintJobStatus.Failed : PrintJobStatus.Pending;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (job.Status == PrintJobStatus.Failed)
        {
            await _notifications.RaiseAsync(job.TenantId, new NotificationDraft
            {
                Type = "PrintFailed",
                Severity = NotificationSeverity.Critical,
                Title = $"KOT did not print: order #{job.Order?.OrderNumber}",
                Message = $"Check the printer at the Print Station, then reprint from Orders. ({job.ErrorMessage})",
                Link = "/admin/orders",
                DedupKey = $"print:{job.Id}"
            });
        }
        else
        {
            await _hub.Clients.Group(job.TenantId).SendAsync("PrintJobCreated");
        }
    }
}
