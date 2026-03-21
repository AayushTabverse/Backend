using menu_backend.Data;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class PrintService : IPrintService
{
    private readonly AppDbContext _db;

    public PrintService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreatePrintJobAsync(Guid orderId, string tenantId)
    {
        var job = new PrintJob
        {
            TenantId = tenantId,
            OrderId = orderId,
            Status = PrintJobStatus.Pending
        };

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync();
    }

    public async Task<List<PrintJob>> GetPendingPrintJobsAsync()
    {
        return await _db.PrintJobs
            .Include(p => p.Order)
                .ThenInclude(o => o!.Items)
            .Include(p => p.Order)
                .ThenInclude(o => o!.Table)
            .Where(p => p.Status == PrintJobStatus.Pending && p.RetryCount < p.MaxRetries)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
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
        var job = await _db.PrintJobs.FindAsync(printJobId)
            ?? throw new KeyNotFoundException("Print job not found.");

        job.RetryCount++;
        job.ErrorMessage = errorMessage;
        job.Status = job.RetryCount >= job.MaxRetries ? PrintJobStatus.Failed : PrintJobStatus.Pending;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
