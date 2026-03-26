using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class CustomerDue : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    [MaxLength(20)]
    public string? BillNumber { get; set; }

    /// <summary>Total bill amount (after discount)</summary>
    public decimal BillAmount { get; set; }

    /// <summary>Amount paid by customer</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Outstanding due amount</summary>
    public decimal DueAmount { get; set; }

    public bool IsSettled { get; set; } = false;

    public DateTime? SettledAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
