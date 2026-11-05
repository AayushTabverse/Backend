using System.ComponentModel.DataAnnotations;
using menu_backend.DTOs.Order;

namespace menu_backend.DTOs.Hotel;

public class RoomRequest
{
    [Required]
    [MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Floor { get; set; }

    [MaxLength(50)]
    public string? RoomType { get; set; }

    public bool IsActive { get; set; } = true;
}

public class RoomResponse
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? RoomType { get; set; }
    public bool IsActive { get; set; }
    /// <summary>The guest checked in now, if any.</summary>
    public StayResponse? Stay { get; set; }
    /// <summary>What the current stay owes so far (unbilled orders).</summary>
    public decimal OpenBalance { get; set; }
    public int OpenOrderCount { get; set; }
}

public class CheckInRequest
{
    [Required]
    [MaxLength(100)]
    public string GuestName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? GuestMobile { get; set; }

    public DateTime? ExpectedCheckOutAt { get; set; }

    /// <summary>Give the guest a PIN that room QR orders need (recommended).</summary>
    public bool RequirePin { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class StayResponse
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string? GuestMobile { get; set; }
    public string? Pin { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CheckInAt { get; set; }
    public DateTime? ExpectedCheckOutAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public string? BillNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A stay's room bill so far.</summary>
public class FolioResponse
{
    public StayResponse Stay { get; set; } = new();
    public List<OrderResponse> Orders { get; set; } = new();
    /// <summary>Not billed yet: due at checkout.</summary>
    public decimal OpenBalance { get; set; }
}

public class CheckOutResponse
{
    public StayResponse Stay { get; set; } = new();
    /// <summary>Null when the stay owed nothing.</summary>
    public TableSessionSummary? Bill { get; set; }
}

/// <summary>What the room QR page needs. No guest details beyond a first name.</summary>
public class PublicRoomResponse
{
    public Guid RoomId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public bool CheckedIn { get; set; }
    public bool PinRequired { get; set; }
    public string? GuestFirstName { get; set; }
    public bool RoomServiceEnabled { get; set; }
}
