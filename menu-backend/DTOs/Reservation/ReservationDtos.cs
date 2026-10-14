using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Reservation;

public class CreateReservationRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress]
    public string? CustomerEmail { get; set; }

    [Range(1, 100)]
    public int PartySize { get; set; } = 2;

    /// <summary>Booking time (UTC, ISO 8601).</summary>
    [Required]
    public DateTime ReservedFor { get; set; }

    [Range(15, 480)]
    public int DurationMinutes { get; set; } = 90;

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Optional tables to block (several = merged).</summary>
    public List<Guid>? TableIds { get; set; }
}

public class UpdateReservationRequest
{
    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? CustomerEmail { get; set; }

    [Range(1, 100)]
    public int? PartySize { get; set; }

    public DateTime? ReservedFor { get; set; }

    [Range(15, 480)]
    public int? DurationMinutes { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Replaces the assigned tables when set (empty list = no tables).</summary>
    public List<Guid>? TableIds { get; set; }
}

public class AcceptReservationRequest
{
    public List<Guid>? TableIds { get; set; }

    [MaxLength(300)]
    public string? Message { get; set; }
}

public class RejectReservationRequest
{
    [MaxLength(300)]
    public string? Message { get; set; }
}

public class SeatReservationRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> TableIds { get; set; } = new();

    public bool Force { get; set; }
}

public class ReservationTableInfo
{
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

public class ReservationResponse
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int PartySize { get; set; }
    public DateTime ReservedFor { get; set; }
    public int DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ResponseMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<ReservationTableInfo> Tables { get; set; } = new();
}

public class ReservationSettingsResponse
{
    public bool OnlineReservationsEnabled { get; set; }
    public int GraceMinutes { get; set; }
}

public class UpdateReservationSettingsRequest
{
    public bool? OnlineReservationsEnabled { get; set; }

    [Range(0, 240)]
    public int? GraceMinutes { get; set; }
}

// ── Public (restaurant website) ──

public class PublicReservationRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress]
    public string? CustomerEmail { get; set; }

    [Range(1, 30)]
    public int PartySize { get; set; } = 2;

    [Required]
    public DateTime ReservedFor { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class PublicReservationInfoResponse
{
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool OnlineReservationsEnabled { get; set; }
    public string? Phone { get; set; }
}

/// <summary>What the customer sees on their booking page. No mobile number.</summary>
public class PublicReservationStatusResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? RestaurantPhone { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateTime ReservedFor { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResponseMessage { get; set; }
    public bool CanCancel { get; set; }
}
