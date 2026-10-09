using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Staff;

// ── Attendance ──

public class AttendanceEntryRequest
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Present, Absent, HalfDay, Leave — or null to clear the day.
    /// </summary>
    public string? Status { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class SaveAttendanceRequest
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public List<AttendanceEntryRequest> Entries { get; set; } = new();
}

public class DailyAttendanceResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? MarkedBy { get; set; }
}

// ── Salary ──

public class UpdateSalaryRequest
{
    [Range(0, 10_000_000)]
    public decimal MonthlySalary { get; set; }
}

public class MonthlySalaryResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; }
    public decimal MonthlySalary { get; set; }
    public int DaysInMonth { get; set; }

    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int HalfDays { get; set; }
    public int LeaveDays { get; set; }
    public int UnmarkedDays { get; set; }

    /// <summary>
    /// MonthlySalary / DaysInMonth × (Absent + 0.5 × HalfDay).
    /// </summary>
    public decimal Deduction { get; set; }

    /// <summary>
    /// MonthlySalary − Deduction.
    /// </summary>
    public decimal Earned { get; set; }

    public decimal AdvancePaid { get; set; }
    public decimal SalaryPaid { get; set; }
    public decimal BonusPaid { get; set; }

    /// <summary>
    /// Earned − AdvancePaid − SalaryPaid. Negative means overpaid.
    /// </summary>
    public decimal Balance { get; set; }
}

// ── Payments ──

public class CreateStaffPaymentRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Required]
    public string Type { get; set; } = "Salary"; // Salary, Advance, Bonus

    [Range(0.01, 10_000_000)]
    public decimal Amount { get; set; }

    public string Mode { get; set; } = "Cash"; // Cash, Upi, BankTransfer

    public DateOnly? PaidOn { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class StaffPaymentResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Mode { get; set; } = string.Empty;
    public DateOnly PaidOn { get; set; }
    public string? Notes { get; set; }
    public string? RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
