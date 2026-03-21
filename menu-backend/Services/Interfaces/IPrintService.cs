using menu_backend.Models;

namespace menu_backend.Services.Interfaces;

public interface IPrintService
{
    Task CreatePrintJobAsync(Guid orderId, string tenantId);
    Task<List<PrintJob>> GetPendingPrintJobsAsync();
    Task MarkPrintedAsync(Guid printJobId);
    Task MarkFailedAsync(Guid printJobId, string errorMessage);
}
