using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Order;

public class CreateOrderRequest
{
    /// <summary>Required for dine-in; empty for takeaway.</summary>
    public Guid? TableId { get; set; }

    /// <summary>"DineIn" (default), "Takeaway" or "RoomService".</summary>
    [MaxLength(20)]
    public string? Type { get; set; }

    /// <summary>Takeaway: who to call when it's ready (required when the customer orders by QR).</summary>
    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    /// <summary>Room service: the room ordering.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>Room service from the room QR: the PIN the guest got at check-in.</summary>
    [MaxLength(6)]
    public string? RoomPin { get; set; }

    /// <summary>Room service: add to the room bill (paid at checkout) instead of paying on delivery.</summary>
    public bool ChargeToRoom { get; set; }

    /// <summary>Customer's phone location, when the restaurant only takes QR orders from inside.</summary>
    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    /// <summary>The phone's reported location accuracy, in metres.</summary>
    public double? LocationAccuracy { get; set; }

    public string? SpecialInstructions { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required]
    public Guid MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;

    public string? Modifiers { get; set; }

    public string? Notes { get; set; }

    /// <summary>Pack this item to take away (dine-in customers ordering something for home).</summary>
    public bool IsParcel { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public int? EstimatedMinutes { get; set; }
}

public class ClearTableRequest
{
    /// <summary>
    /// Bill only these orders (one party at a shared table, or takeaway orders). Empty = the whole table.
    /// </summary>
    public List<Guid>? OrderIds { get; set; }

    /// <summary>Loyalty points the customer (by CustomerMobile) uses on this bill; taken off before tax.</summary>
    [Range(0, 1_000_000)]
    public int RedeemPoints { get; set; }

    /// <summary>
    /// How the paid part was paid (cash, UPI, card… several for a split payment). Their total is the
    /// amount paid now; the rest goes on udhaar. Empty = PaidAmount (or the whole bill) in cash.
    /// </summary>
    public List<DTOs.Accounting.PaymentSplit>? Payments { get; set; }

    /// <summary>Saved on the customer for the birthday bonus.</summary>
    public DateOnly? CustomerDateOfBirth { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public string? Notes { get; set; }
}

public class ApplyWheelDiscountRequest
{
    [Required]
    public Guid TableId { get; set; }

    [Required]
    [Range(0, 10000)]
    public decimal DiscountValue { get; set; }

    /// <summary>percent or flat</summary>
    [Required]
    public string DiscountType { get; set; } = "percent";
}

public class OrderResponse
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    /// <summary>Table number; the pickup token for takeaway.</summary>
    public string TableNumber { get; set; } = string.Empty;
    public Guid? TableId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? SpecialInstructions { get; set; }
    public int EstimatedMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? BillNumber { get; set; }
    public string? CustomerSessionId { get; set; }
    public decimal PackagingCharge { get; set; }
    /// <summary>Loyalty points redeemed (₹), before tax.</summary>
    public decimal LoyaltyDiscount { get; set; }
    public string? TokenNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? StayId { get; set; }
    public bool ChargeToRoom { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public PaymentSummary? Payment { get; set; }
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Modifiers { get; set; }
    public string? Notes { get; set; }
    public bool IsParcel { get; set; }
    /// <summary>Per unit.</summary>
    public decimal PackagingCharge { get; set; }
}

public class PaymentSummary
{
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}

public class LiveOrdersResponse
{
    public int TotalPending { get; set; }
    public int TotalPreparing { get; set; }
    public int TotalReady { get; set; }
    public List<OrderResponse> Orders { get; set; } = new();
}

/// <summary>
/// Summary of all active orders at a table (for waiter "table session" view).
/// </summary>
public class TableSessionSummary
{
    public Guid? TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableLabel { get; set; }
    public int ActiveOrderCount { get; set; }
    public decimal GrandSubTotal { get; set; }
    public decimal GrandPackaging { get; set; }
    /// <summary>Loyalty points redeemed (₹), taken off before tax.</summary>
    public decimal GrandLoyaltyDiscount { get; set; }
    public decimal GrandTax { get; set; }
    public decimal GrandDiscount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public List<OrderResponse> Orders { get; set; } = new();

    /// <summary>Set on a just-settled bill when the customer is in the loyalty program.</summary>
    public DTOs.Loyalty.LoyaltyOutcome? Loyalty { get; set; }
}

/// <summary>
/// A bill groups all orders from a single table session (created when table is cleared).
/// </summary>
 public class BillResponse
{
    public string BillNumber { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public string? TableLabel { get; set; }
    public int OrderCount { get; set; }
    public int TotalItems { get; set; }
    public decimal SubTotal { get; set; }
    public decimal PackagingCharge { get; set; }
    public decimal LoyaltyDiscount { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }
    public decimal Tax { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<OrderResponse> Orders { get; set; } = new();
    /// <summary>Changes made after the bill was printed (newest first). Empty if never edited.</summary>
    public List<BillAdjustmentResponse> Adjustments { get; set; } = new();
}

public class BillAdjustmentResponse
{
    public string ItemName { get; set; } = string.Empty;
    public int QuantityRemoved { get; set; }
    public decimal AmountRemoved { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class RemoveBillItemsRequest
{
    [Required]
    [MinLength(1)]
    public List<RemoveBillItemLine> Items { get; set; } = new();

    [MaxLength(200)]
    public string? Reason { get; set; }
}

public class RemoveBillItemLine
{
    [Required]
    public Guid OrderItemId { get; set; }

    /// <summary>How many to take off (the whole line when it equals the line's quantity).</summary>
    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}

public class RemoveBillItemsResponse
{
    public BillResponse Bill { get; set; } = new();
    public decimal PreviousTotal { get; set; }
    /// <summary>Money to give back: the customer had already paid more than the new total.</summary>
    public decimal RefundAmount { get; set; }
}

public class PaginatedBillsResponse
{
    public List<BillResponse> Bills { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class CreatePastBillRequest
{
    [Required]
    public DateTime BillDate { get; set; }

    [Required]
    public Guid TableId { get; set; }

    [Required]
    [MinLength(1)]
    public List<PastBillItem> Items { get; set; } = new();

    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public string? Notes { get; set; }
}

public class PastBillItem
{
    [Required]
    public Guid MenuItemId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}
