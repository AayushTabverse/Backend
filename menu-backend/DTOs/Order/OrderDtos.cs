using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Order;

public class CreateOrderRequest
{
    [Required]
    public Guid TableId { get; set; }

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
}

public class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public int? EstimatedMinutes { get; set; }
}

public class ClearTableRequest
{
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public string? Notes { get; set; }
}

public class OrderResponse
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public Guid TableId { get; set; }
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
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableLabel { get; set; }
    public int ActiveOrderCount { get; set; }
    public decimal GrandSubTotal { get; set; }
    public decimal GrandTax { get; set; }
    public decimal GrandDiscount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public List<OrderResponse> Orders { get; set; } = new();
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
    public decimal Tax { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<OrderResponse> Orders { get; set; } = new();
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
