using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    Cash = 0,
    Online = 1,
    Card = 2,
    UPI = 3
}

public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }

    public decimal Amount { get; set; }

    [Required]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Required]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(100)]
    public string? RazorpayOrderId { get; set; }

    [MaxLength(100)]
    public string? RazorpayPaymentId { get; set; }

    [MaxLength(200)]
    public string? RazorpaySignature { get; set; }

    [MaxLength(100)]
    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }

    // Navigation
    public Order? Order { get; set; }
}
