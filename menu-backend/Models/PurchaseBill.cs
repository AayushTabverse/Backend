using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

/// <summary>A supplier's invoice (vegetables, gas, packaging…), for the purchase register and Tally.</summary>
public class PurchaseBill : BaseEntity
{
    [Required]
    [MaxLength(150)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? SupplierGstin { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Invoice date (restaurant-local calendar day, stored at midnight).</summary>
    public DateTime InvoiceDate { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    /// <summary>Purchase ledger it books to (e.g. "Food Purchases", "Gas & Fuel"); empty = the default.</summary>
    [MaxLength(100)]
    public string? ExpenseLedger { get; set; }

    public decimal TaxableAmount { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    /// <summary>Supplier from another state charges IGST instead of CGST + SGST.</summary>
    public decimal Igst { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Payment account it was paid from; null = still owed to the supplier (on credit).</summary>
    [MaxLength(40)]
    public string? PaidAccountKey { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(100)]
    public string? RecordedBy { get; set; }
}
