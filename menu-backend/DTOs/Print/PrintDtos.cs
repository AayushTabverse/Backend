namespace menu_backend.DTOs.Print;

/// <summary>A kitchen order ticket waiting at the Print Station.</summary>
public class KotTicketResponse
{
    public Guid JobId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    /// <summary>DineIn, Takeaway or RoomService.</summary>
    public string OrderType { get; set; } = "DineIn";
    /// <summary>Table number, takeaway token or room number: what the kitchen calls it by.</summary>
    public string Destination { get; set; } = string.Empty;
    public string? SpecialInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Copies { get; set; } = 1;
    /// <summary>True when staff asked for this copy (not the automatic one).</summary>
    public bool IsReprint { get; set; }
    public List<KotTicketItem> Items { get; set; } = new();
}

public class KotTicketItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Modifiers { get; set; }
    public string? Notes { get; set; }
    public bool IsParcel { get; set; }
}
