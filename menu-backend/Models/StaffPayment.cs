using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum StaffPaymentType
{
    Salary = 0,
    Advance = 1,
    Bonus = 2
}

public enum StaffPaymentMode
{
    Cash = 0,
    Upi = 1,
    BankTransfer = 2
}

/// <summary>
/// Money paid to a staff member, attributed to a salary month.
/// Salary and Advance payouts reduce the month's balance; Bonus is paid on top.
/// </summary>
public class StaffPayment : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public StaffPaymentType Type { get; set; } = StaffPaymentType.Salary;

    public decimal Amount { get; set; }

    public StaffPaymentMode Mode { get; set; } = StaffPaymentMode.Cash;

    public DateTime PaidOn { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? RecordedBy { get; set; }
}
