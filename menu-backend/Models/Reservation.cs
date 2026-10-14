using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum ReservationStatus
{
    /// <summary>Requested from the website, waiting for the restaurant to accept or reject.</summary>
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Cancelled = 3,
    /// <summary>Party arrived and holds its tables.</summary>
    Seated = 4,
    Completed = 5,
    NoShow = 6
}

public enum ReservationSource
{
    Staff = 0,
    Website = 1
}

/// <summary>
/// A table booked for a later time. Website requests start Pending; staff bookings start Confirmed.
/// When the party arrives it is seated through a waitlist entry, which holds the tables until billed.
/// </summary>
public class Reservation : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    public int PartySize { get; set; } = 2;

    /// <summary>Booking time (UTC).</summary>
    public DateTime ReservedFor { get; set; }

    /// <summary>How long the tables are blocked for this booking.</summary>
    public int DurationMinutes { get; set; } = 90;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public ReservationSource Source { get; set; } = ReservationSource.Staff;

    /// <summary>Message to the customer, e.g. the reason a request was rejected.</summary>
    [MaxLength(300)]
    public string? ResponseMessage { get; set; }

    public DateTime? RespondedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>Waitlist entry that holds the tables once the party is seated.</summary>
    public Guid? WaitlistEntryId { get; set; }

    // Navigation
    public ICollection<ReservationTable> Tables { get; set; } = new List<ReservationTable>();
}

/// <summary>Table assigned to a reservation (several = merged for a large party).</summary>
public class ReservationTable : BaseEntity
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    public Guid TableId { get; set; }

    // Navigation
    public Reservation? Reservation { get; set; }
    public RestaurantTable? Table { get; set; }
}
