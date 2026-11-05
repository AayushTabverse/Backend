using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Accounting;

/// <summary>Where money goes: cash drawer, a UPI/bank account, a card machine's bank…</summary>
public class PaymentAccount
{
    [Required, MaxLength(40)]
    public string Key { get; set; } = string.Empty;

    /// <summary>What staff see on the bill screen, e.g. "UPI (HDFC)".</summary>
    [Required, MaxLength(60)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Cash, Upi, Card or Bank.</summary>
    [Required, MaxLength(10)]
    public string Kind { get; set; } = "Cash";

    /// <summary>Tally ledger name, e.g. "Cash" or "HDFC Bank".</summary>
    [Required, MaxLength(100)]
    public string Ledger { get; set; } = string.Empty;
}

/// <summary>Tally ledger names for each kind of entry (match the CA's company in Tally).</summary>
public class AccountingLedgers
{
    [MaxLength(100)] public string Sales { get; set; } = "Food Sales";
    [MaxLength(100)] public string Packing { get; set; } = "Packing Charges";
    [MaxLength(100)] public string Cgst { get; set; } = "Output CGST";
    [MaxLength(100)] public string Sgst { get; set; } = "Output SGST";
    [MaxLength(100)] public string ServiceCharge { get; set; } = "Service Charge";
    [MaxLength(100)] public string Discount { get; set; } = "Discount Allowed";
    [MaxLength(100)] public string LoyaltyDiscount { get; set; } = "Loyalty Discount";
    [MaxLength(100)] public string RoundOff { get; set; } = "Round Off";
    [MaxLength(100)] public string Salary { get; set; } = "Salaries & Wages";
    [MaxLength(100)] public string Purchases { get; set; } = "Purchases";
    [MaxLength(100)] public string InputCgst { get; set; } = "Input CGST";
    [MaxLength(100)] public string InputSgst { get; set; } = "Input SGST";
    [MaxLength(100)] public string InputIgst { get; set; } = "Input IGST";
}

public class AccountingSettings
{
    [MaxLength(15)]
    [RegularExpression(@"^$|^(0[1-9]|[12][0-9]|3[0-8])[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "Enter a valid GSTIN (15 characters, starting with the 2-digit state code).")]
    public string? Gstin { get; set; }

    [MaxLength(50)]
    public string? StateName { get; set; }

    public List<PaymentAccount> PaymentAccounts { get; set; } = new();

    public AccountingLedgers Ledgers { get; set; } = new();

    /// <summary>Account salaries paid by UPI/bank transfer come from (cash salaries use the cash account).</summary>
    [MaxLength(40)]
    public string? SalaryBankAccountKey { get; set; }
}

/// <summary>Part of a payment at billing: amount into one payment account.</summary>
public class PaymentSplit
{
    [Required, MaxLength(40)]
    public string AccountKey { get; set; } = "cash";

    [Range(0, 10_000_000)]
    public decimal Amount { get; set; }
}

/// <summary>One day's accounts, as exported to Tally.</summary>
public class AccountingDay
{
    public DateOnly Date { get; set; }
    public int Bills { get; set; }
    public decimal Sales { get; set; }
    public decimal Packing { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal Discounts { get; set; }
    public decimal LoyaltyDiscount { get; set; }
    public decimal BillTotal { get; set; }
    /// <summary>Money in at billing, by payment account label.</summary>
    public Dictionary<string, decimal> Received { get; set; } = new();
    public decimal UdhaarGiven { get; set; }
    public decimal UdhaarReceived { get; set; }
    public decimal Refunds { get; set; }
    public decimal Salaries { get; set; }
    public decimal Purchases { get; set; }
    public decimal SupplierPayments { get; set; }
    /// <summary>Sales voucher difference booked to Round Off (should be paise).</summary>
    public decimal RoundOff { get; set; }
}

public class AccountingSummaryResponse
{
    public List<AccountingDay> Days { get; set; } = new();
    public AccountingDay Total { get; set; } = new();
    /// <summary>Problems a CA should know about (e.g. a day that doesn't balance).</summary>
    public List<string> Warnings { get; set; } = new();
}

public class PurchaseBillRequest
{
    [Required, MaxLength(150)] public string SupplierName { get; set; } = string.Empty;
    [MaxLength(15)]
    [RegularExpression(@"^$|^(0[1-9]|[12][0-9]|3[0-8])[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "Enter a valid supplier GSTIN (15 characters, starting with the 2-digit state code) or leave it empty.")]
    public string? SupplierGstin { get; set; }
    [Required, MaxLength(50)] public string InvoiceNumber { get; set; } = string.Empty;
    [Required] public DateOnly InvoiceDate { get; set; }
    [MaxLength(300)] public string? Description { get; set; }
    [MaxLength(100)] public string? ExpenseLedger { get; set; }
    [Range(0, 100_000_000)] public decimal TaxableAmount { get; set; }
    [Range(0, 10_000_000)] public decimal Cgst { get; set; }
    [Range(0, 10_000_000)] public decimal Sgst { get; set; }
    [Range(0, 10_000_000)] public decimal Igst { get; set; }
    /// <summary>Paid now from this account; empty = on credit.</summary>
    [MaxLength(40)] public string? PaidAccountKey { get; set; }
}

public class PayPurchaseRequest
{
    [Required, MaxLength(40)] public string AccountKey { get; set; } = "cash";
    public DateOnly? PaidOn { get; set; }
}

public class PurchaseBillResponse
{
    public Guid Id { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierGstin { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public string? Description { get; set; }
    public string? ExpenseLedger { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaidAccountKey { get; set; }
    public DateTime? PaidAt { get; set; }
}
