using menu_backend.DTOs.Notification;

namespace menu_backend.Services.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification, or updates the unresolved one with the same DedupKey. An acknowledged
    /// alert only blinks again if its severity goes up (e.g. low stock → out of stock).
    /// </summary>
    Task RaiseAsync(string tenantId, NotificationDraft draft);

    /// <summary>The condition behind <paramref name="dedupKey"/> went away: close its open notification.</summary>
    Task ResolveAsync(string tenantId, string dedupKey, string resolvedBy);

    Task<NotificationSummaryResponse> GetSummaryAsync();

    /// <summary><paramref name="filter"/>: "open" (unacknowledged) or "all".</summary>
    Task<PaginatedNotificationsResponse> GetNotificationsAsync(string filter, int page, int pageSize);

    Task AcknowledgeAsync(Guid id, string? userName);
    Task<int> AcknowledgeAllAsync(string? userName);

    /// <summary>Once a day (from 9 AM local), reminds each restaurant of items still low or out of stock.</summary>
    Task<int> SendDailyStockRemindersAsync();
}
