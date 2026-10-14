using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum WaitlistStatus
{
    Waiting = 0,
    Notified = 1,
    Seated = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum WaitlistSource
{
    Staff = 0,
    SelfCheckIn = 1,
    /// <summary>A reservation party that arrived and was seated (holds tables like a waitlist party).</summary>
    Reservation = 2
}

/// <summary>
/// A walk-in party waiting for a table. While Seated, the party holds its tables
/// (one, or several merged) until the table is cleared or released.
/// </summary>
public class WaitlistEntry : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    public int PartySize { get; set; } = 2;

    [MaxLength(300)]
    public string? Notes { get; set; }

    /// <summary>Daily running token (#1, #2...) staff can call out.</summary>
    public int TokenNumber { get; set; }

    public int? QuotedWaitMinutes { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;

    public WaitlistSource Source { get; set; } = WaitlistSource.Staff;

    public DateTime? NotifiedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public ICollection<WaitlistEntryTable> Tables { get; set; } = new List<WaitlistEntryTable>();
}

/// <summary>
/// Table assigned to a waitlist party. A party seated across merged tables has several rows;
/// orders go on the primary table.
/// </summary>
public class WaitlistEntryTable : BaseEntity
{
    [Required]
    public Guid WaitlistEntryId { get; set; }

    [Required]
    public Guid TableId { get; set; }

    public bool IsPrimary { get; set; }

    // Navigation
    public WaitlistEntry? WaitlistEntry { get; set; }
    public RestaurantTable? Table { get; set; }
}
