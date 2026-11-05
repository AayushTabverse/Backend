using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Walkie;

/// <summary>A walkie-talkie message as staff screens see it (audio is fetched separately).</summary>
public class WalkieMessageResponse
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Channel { get; set; } = "All";
    public Guid? RecipientUserId { get; set; }
    public string? RecipientName { get; set; }
    public string? QuickKey { get; set; }
    public string? QuickTable { get; set; }
    public bool HasAudio { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuickCallRequest
{
    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = "All";

    public Guid? RecipientUserId { get; set; }

    [Required]
    [MaxLength(40)]
    public string QuickKey { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? QuickTable { get; set; }
}

/// <summary>Someone a message can be sent to directly.</summary>
public class WalkieContact
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
