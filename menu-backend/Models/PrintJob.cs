using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum PrintJobStatus
{
    Pending = 0,
    Printing = 1,
    Completed = 2,
    Failed = 3
}

public class PrintJob : BaseEntity
{
    public Guid OrderId { get; set; }

    [Required]
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;

    public int RetryCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 3;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime? PrintedAt { get; set; }

    // Navigation
    public Order? Order { get; set; }
}
