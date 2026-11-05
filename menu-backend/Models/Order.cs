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
    Takeaway = 1,
    RoomService = 2
}

public class Order : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Null for takeaway and room service orders.</summary>
    public Guid? TableId { get; set; }

    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public OrderType Type { get; set; } = OrderType.DineIn;

    /// <summary>Food total (item prices × quantities), before packing, tax and discount.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Packing for parcel items plus the flat takeaway packing fee. Taxed like the food.</summary>
    public decimal PackagingCharge { get; set; }

    /// <summary>Loyalty points redeemed on this order (₹), taken off before tax.</summary>
    public decimal LoyaltyDiscount { get; set; }

    /// <summary>The restaurant's flat per-order takeaway packing fee, kept for recalculations.</summary>
    public decimal PackingFee { get; set; }

    public decimal Tax { get; set; }

    /// <summary>Discount amount applied when clearing the table</summary>
    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [MaxLength(500)]
    public string? SpecialInstructions { get; set; }

    public DateTime? AcceptedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int EstimatedMinutes { get; set; } = 20;

    [MaxLength(20)]
    public string? BillNumber { get; set; }

    /// <summary>Customer browser session ID — used to scope order visibility per QR scan session</summary>
    [MaxLength(50)]
    public string? CustomerSessionId { get; set; }

    /// <summary>Takeaway pickup token shown to the customer and called out at the counter, e.g. "T-012".</summary>
    [MaxLength(20)]
    public string? TokenNumber { get; set; }

    /// <summary>Takeaway customer (who to call when it's ready).</summary>
    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    /// <summary>Takeaway handed over to the customer.</summary>
    public DateTime? PickedUpAt { get; set; }

    // ── Room service (TokenNumber holds the room number for display) ──

    public Guid? RoomId { get; set; }

    /// <summary>The guest's stay the order belongs to.</summary>
    public Guid? StayId { get; set; }

    /// <summary>Added to the room bill and paid at checkout, instead of paying on delivery.</summary>
    public bool ChargeToRoom { get; set; }

    /// <summary>Attendant collected it from the kitchen.</summary>
    public DateTime? DispatchedAt { get; set; }

    /// <summary>Attendant handed it to the guest.</summary>
    public DateTime? DeliveredAt { get; set; }

    // Navigation
    public RestaurantTable? Table { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
    public ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();
}
