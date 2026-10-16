using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

/// <summary>
/// Audit trail of a change made to a bill after it was printed (e.g. an item the customer says
/// they never got). One row per item line changed.
/// </summary>
public class BillAdjustment : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string BillNumber { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    public int QuantityRemoved { get; set; }

    /// <summary>Amount taken off the bill for this line, including tax.</summary>
    public decimal AmountRemoved { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    [MaxLength(100)]
    public string? ChangedBy { get; set; }
}
