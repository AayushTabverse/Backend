using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum UserRole
{
    SuperAdmin = 0,
    RestaurantAdmin = 1,
    Waiter = 2,
    Kitchen = 3
}

public class User : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    public UserRole Role { get; set; } = UserRole.Waiter;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}
