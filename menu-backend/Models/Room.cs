using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

/// <summary>Hotel room. Its QR opens the menu for in-room dining while a guest is checked in.</summary>
public class Room : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Floor { get; set; }

    /// <summary>Free text, e.g. "Deluxe", "Suite".</summary>
    [MaxLength(50)]
    public string? RoomType { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Stay> Stays { get; set; } = new List<Stay>();
}

public enum StayStatus
{
    Active = 0,
    CheckedOut = 1
}

/// <summary>
/// A guest's stay in a room (no booking: created at check-in). Room service orders charged to the
/// room collect here and are billed together at checkout.
/// </summary>
public class Stay : BaseEntity
{
    public Guid RoomId { get; set; }

    [Required]
    [MaxLength(100)]
    public string GuestName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? GuestMobile { get; set; }

    /// <summary>Given to the guest at check-in; room QR orders need it, so a visitor can't order on the room's bill.</summary>
    [MaxLength(6)]
    public string? Pin { get; set; }

    public StayStatus Status { get; set; } = StayStatus.Active;

    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedCheckOutAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }

    /// <summary>Bill for the room charges, set at checkout.</summary>
    [MaxLength(20)]
    public string? BillNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Room? Room { get; set; }
}
