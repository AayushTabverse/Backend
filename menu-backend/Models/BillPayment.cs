using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum MoneyInSource
{
    /// <summary>Paid when the bill was settled.</summary>
    Bill = 0,
    /// <summary>Udhaar paid later.</summary>
    Udhaar = 1,
    /// <summary>Money given back after a bill was reduced (negative amount).</summary>
    Refund = 2
}

/// <summary>
/// Money received for a bill, in one payment account (cash, a UPI/bank account, card machine…).
/// A split payment is several rows. Accounting exports and the cash report read these.
/// </summary>
public class BillPayment : BaseEntity
{
    [MaxLength(20)]
    public string? BillNumber { get; set; }

    /// <summary>Key of the restaurant's payment account (see AccountingSettings.PaymentAccounts).</summary>
    [Required]
    [MaxLength(40)]
    public string AccountKey { get; set; } = "cash";

    /// <summary>Negative for refunds.</summary>
    public decimal Amount { get; set; }

    public MoneyInSource Source { get; set; } = MoneyInSource.Bill;

    /// <summary>For udhaar payments: the due it reduced (and so the customer).</summary>
    public Guid? CustomerDueId { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? RecordedBy { get; set; }
}
