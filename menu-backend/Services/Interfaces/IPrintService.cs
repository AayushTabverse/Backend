using menu_backend.DTOs.Print;

namespace menu_backend.Services.Interfaces;

public interface IPrintService
{
    /// <summary>
    /// Queues the order's KOT for the Print Station when the restaurant auto-prints KOTs at this
    /// moment (<paramref name="trigger"/>: "OnOrder" or "OnAccept"). At most one automatic KOT per order.
    /// </summary>
    Task QueueKotAsync(Guid orderId, string tenantId, string trigger);

    /// <summary>Staff asked to (re)print this order's KOT.</summary>
    Task ReprintKotAsync(Guid orderId);

    /// <summary>Recent jobs not printed yet (jobs older than a couple of hours are skipped, not printed late).</summary>
    Task<List<KotTicketResponse>> GetPendingAsync();

    /// <summary>Takes the job for one Print Station. False if another station already has it.</summary>
    Task<bool> ClaimAsync(Guid printJobId);

    Task MarkPrintedAsync(Guid printJobId);
    Task MarkFailedAsync(Guid printJobId, string errorMessage);
}
