using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Waitlist;

public class CreateWaitlistEntryRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    [Range(1, 100)]
    public int PartySize { get; set; } = 2;

    [MaxLength(300)]
    public string? Notes { get; set; }

    [Range(0, 600)]
    public int? QuotedWaitMinutes { get; set; }
}

public class UpdateWaitlistEntryRequest
{
    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [MaxLength(20)]
    public string? CustomerMobile { get; set; }

    [Range(1, 100)]
    public int? PartySize { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [Range(0, 600)]
    public int? QuotedWaitMinutes { get; set; }
}

public class SeatWaitlistEntryRequest
{
    /// <summary>One table, or several to merge. The first one is the primary (orders go there).</summary>
    [Required]
    [MinLength(1)]
    public List<Guid> TableIds { get; set; } = new();

    /// <summary>Seat even if the combined capacity is smaller than the party.</summary>
    public bool Force { get; set; }
}

public class WaitlistTableInfo
{
    public Guid TableId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsPrimary { get; set; }
}

public class WaitlistEntryResponse
{
    public Guid Id { get; set; }
    public int TokenNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerMobile { get; set; }
    public int PartySize { get; set; }
    public string? Notes { get; set; }
    public int? QuotedWaitMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    /// <summary>Estimated minutes until a table, for parties still waiting.</summary>
    public int? EstimatedWaitMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<WaitlistTableInfo> Tables { get; set; } = new();
}

public class WaitlistStatsResponse
{
    public int Waiting { get; set; }
    public int PeopleWaiting { get; set; }
    public int SeatedToday { get; set; }
    public int CancelledToday { get; set; }
    public int NoShowToday { get; set; }
    public int? AverageWaitMinutesToday { get; set; }
    /// <summary>Estimated wait for a party joining now (used to pre-fill the quote).</summary>
    public int EstimatedWaitForNewParty { get; set; }
}

// ── Settings ──

public class WaitlistSettingsResponse
{
    public bool SelfCheckInEnabled { get; set; }
    public int NoShowMinutes { get; set; }
    public bool ShowTableStatus { get; set; }
}

public class UpdateWaitlistSettingsRequest
{
    public bool? SelfCheckInEnabled { get; set; }

    [Range(0, 120)]
    public int? NoShowMinutes { get; set; }

    public bool? ShowTableStatus { get; set; }
}

// ── Public (customer-facing) ──

public class PublicJoinWaitlistRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [Range(1, 30)]
    public int PartySize { get; set; } = 2;
}

public class PublicWaitlistInfoResponse
{
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool SelfCheckInEnabled { get; set; }
    /// <summary>The entrance QR page can also take bookings for later.</summary>
    public bool OnlineReservationsEnabled { get; set; }
    public int PartiesWaiting { get; set; }
    public int EstimatedWaitMinutes { get; set; }
    /// <summary>Live table board, when the restaurant shares it.</summary>
    public List<PublicTableStatus>? Tables { get; set; }
}

/// <summary>One table on the customer-facing board. Never includes who is sitting there.</summary>
public class PublicTableStatus
{
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    /// <summary>Free, Occupied or Reserved.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>For reserved tables: when the booking starts.</summary>
    public DateTime? ReservedAt { get; set; }
}

/// <summary>What a waitlisted customer sees on their status page. No mobile numbers.</summary>
public class PublicWaitlistStatusResponse
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int TokenNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>1-based place in line while waiting.</summary>
    public int? Position { get; set; }
    public int? EstimatedWaitMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public List<string> TableNumbers { get; set; } = new();
    /// <summary>Live table board, when the restaurant shares it.</summary>
    public List<PublicTableStatus>? Tables { get; set; }
}

// ── Analytics ──

public class WaitlistAnalyticsResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalParties { get; set; }
    public int TotalGuests { get; set; }
    public int Seated { get; set; }
    public int Cancelled { get; set; }
    public int NoShows { get; set; }
    public int SelfCheckIns { get; set; }
    public int? AverageWaitMinutes { get; set; }
    public int? MedianWaitMinutes { get; set; }
    /// <summary>Share of called parties that never came (0–100).</summary>
    public double NoShowRate { get; set; }
    /// <summary>Share of parties that left before being seated (0–100).</summary>
    public double WalkAwayRate { get; set; }
    public int? BusiestHour { get; set; }
    public List<WaitlistHourBucket> ByHour { get; set; } = new();
    public List<WaitlistPartySizeBucket> ByPartySize { get; set; } = new();
    public List<WaitlistDayBucket> ByDay { get; set; } = new();
}

public class WaitlistHourBucket
{
    /// <summary>Restaurant-local hour of arrival, 0–23.</summary>
    public int Hour { get; set; }
    public int Parties { get; set; }
    public int? AverageWaitMinutes { get; set; }
}

public class WaitlistPartySizeBucket
{
    /// <summary>"1–2", "3–4", "5–6", "7+".</summary>
    public string Label { get; set; } = string.Empty;
    public int Parties { get; set; }
    public int Seated { get; set; }
    public int? AverageWaitMinutes { get; set; }
}

public class WaitlistDayBucket
{
    public DateOnly Date { get; set; }
    public int Parties { get; set; }
    public int Seated { get; set; }
    public int? AverageWaitMinutes { get; set; }
}
