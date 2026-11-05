using menu_backend.Models;

namespace menu_backend.DTOs.Notification;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>What the admin layout needs: bell count and the alerts to blink.</summary>
public class NotificationSummaryResponse
{
    public int UnacknowledgedCount { get; set; }

    /// <summary>Unacknowledged Warning/Critical notifications, most severe and newest first.</summary>
    public List<NotificationResponse> ActiveAlerts { get; set; } = new();
}

public class PaginatedNotificationsResponse
{
    public List<NotificationResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Input for <c>INotificationService.RaiseAsync</c>.</summary>
public class NotificationDraft
{
    public string Type { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Link { get; set; }
    public string? DedupKey { get; set; }
}
