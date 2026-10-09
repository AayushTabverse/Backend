using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum UserRole
{
    SuperAdmin = 0,
    RestaurantAdmin = 1,
    Waiter = 2,
    Kitchen = 3,
    Manager = 4,  // Runs the floor and staff; no billing/subscription, settings or marketing
    Cashier = 5,  // Settles bills, handles udhaar
    Captain = 6,  // Senior waiter; takes orders on any table
    Helper = 7    // Payroll only (cleaner, dishwasher, guard…): no email/password, cannot log in
}

public class User : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Login email. Null for payroll-only staff (UserRole.Helper).
    /// </summary>
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// Null for payroll-only staff, who cannot log in.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Free-text job title shown in staff lists and payroll (e.g. "Tandoor cook", "Cleaner").
    /// </summary>
    [MaxLength(100)]
    public string? JobTitle { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    public UserRole Role { get; set; } = UserRole.Waiter;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Fixed monthly salary used for payroll; absences are deducted pro-rata.
    /// </summary>
    public decimal MonthlySalary { get; set; } = 0;

    // Navigation
    public Tenant? Tenant { get; set; }
}
