using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum OrderStatus
{
    Pending = 0,
    Accepted = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Completed = 5,
    Cancelled = 6
}

public enum OrderType
{
    DineIn = 0,
    Takeaway = 1
}

public class Order : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string OrderNumber { get; set; } = string.Empty;

    public Guid TableId { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public OrderType Type { get; set; } = OrderType.DineIn;

    public decimal SubTotal { get; set; }

    public decimal Tax { get; set; }

    public decimal TotalAmount { get; set; }

    [MaxLength(500)]
    public string? SpecialInstructions { get; set; }

    public DateTime? AcceptedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int EstimatedMinutes { get; set; } = 20;

    // Navigation
    public RestaurantTable? Table { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();
}
