using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public class Customer : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    /// <summary>For the birthday bonus (year may be unknown: any year works).</summary>
    public DateOnly? DateOfBirth { get; set; }

    // ── Visit stats and loyalty (kept up to date when bills are settled) ──

    /// <summary>Current loyalty points (sum of the ledger, cached).</summary>
    public int PointsBalance { get; set; }

    public int VisitCount { get; set; }

    public decimal TotalSpent { get; set; }

    public DateTime? LastVisitAt { get; set; }

    /// <summary>Year the birthday bonus was last given, so it's once a year.</summary>
    public int? LastBirthdayBonusYear { get; set; }
}
