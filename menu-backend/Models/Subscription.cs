using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

/// <summary>
/// Maps a tenant to their Razorpay subscription ID.
/// All plan details (status, plan, cycle, dates) are fetched from Razorpay APIs.
/// </summary>
public class TenantSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(36)]
    [Column("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RazorpaySubscriptionId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant? Tenant { get; set; }
}
