using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum NotificationSeverity
{
    Info = 0,     // Notification centre only
    Warning = 1,  // Also blinks on management screens until acknowledged
    Critical = 2
}

/// <summary>
/// Alert or reminder for the owner/manager (low stock, booking requests…). Shown in the
/// notification centre; Warning and Critical ones blink on every admin screen until someone
/// acknowledges them.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>LowStock, OutOfStock, StockReminder, ReservationRequest.</summary>
    [Required]
    [MaxLength(40)]
    public string Type { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    /// <summary>Admin route that deals with it, e.g. /admin/inventory.</summary>
    [MaxLength(200)]
    public string? Link { get; set; }

    /// <summary>
    /// Identifies the condition behind the alert (e.g. "stock:{itemId}"). While a notification for
    /// the key is unresolved, repeats update it instead of piling up new ones.
    /// </summary>
    [MaxLength(100)]
    public string? DedupKey { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    [MaxLength(100)]
    public string? AcknowledgedBy { get; set; }

    /// <summary>The condition went away (item restocked, booking answered).</summary>
    public DateTime? ResolvedAt { get; set; }
}
