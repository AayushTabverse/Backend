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
