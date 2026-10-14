using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Table;

public class CreateTableRequest
{
    [Required]
    [MaxLength(50)]
    public string TableNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Label { get; set; }

    public int Capacity { get; set; } = 4;
}

public class UpdateTableRequest
{
    [MaxLength(50)]
    public string? TableNumber { get; set; }

    [MaxLength(100)]
    public string? Label { get; set; }

    public int? Capacity { get; set; }

    public bool? IsActive { get; set; }
}

public class TableResponse
{
    public Guid Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
    public bool IsOccupied { get; set; }
    public int ActiveOrderCount { get; set; }
    public bool IsCallingWaiter { get; set; }
    public DateTime? WaiterCalledAt { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? QrData { get; set; }
    public List<string>? AssignedWaiterIds { get; set; }
    public List<string>? AssignedWaiterNames { get; set; }

    /// <summary>Set while a waitlist party is seated here (before or during their orders).</summary>
    public Guid? WaitlistEntryId { get; set; }
    public string? WaitlistCustomerName { get; set; }
    public string? WaitlistCustomerMobile { get; set; }
    public int? WaitlistPartySize { get; set; }
    public bool IsPrimaryWaitlistTable { get; set; }

    /// <summary>Other tables merged with this one for the same party.</summary>
    public List<string>? MergedWithTableNumbers { get; set; }

    /// <summary>Set when this table is merged into another (the main table holds the bill).</summary>
    public Guid? MergedIntoTableId { get; set; }
    public string? MergedIntoTableNumber { get; set; }

    /// <summary>On a main table: the tables merged into it.</summary>
    public List<string>? MergedTableNumbers { get; set; }

    /// <summary>Seats including merged tables.</summary>
    public int CombinedCapacity { get; set; }

    /// <summary>Confirmed booking for this table starting within the next hour (or running late).</summary>
    public Guid? ReservationId { get; set; }
    public string? ReservationCustomerName { get; set; }
    public int? ReservationPartySize { get; set; }
    public DateTime? ReservationTime { get; set; }
}

public class MergeTablesRequest
{
    /// <summary>Tables to attach to the main table.</summary>
    [Required]
    [MinLength(1)]
    public List<Guid> TableIds { get; set; } = new();
}

public class TransferTableRequest
{
    [Required]
    public Guid ToTableId { get; set; }
}

public class AssignTablesRequest
{
    [Required]
    public Guid WaiterId { get; set; }

    [Required]
    public List<Guid> TableIds { get; set; } = new();
}

public class WaiterAssignmentResponse
{
    public Guid WaiterId { get; set; }
    public string WaiterName { get; set; } = string.Empty;
    public List<Guid> AssignedTableIds { get; set; } = new();
}
