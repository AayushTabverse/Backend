using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

/// <summary>
/// Walkie-talkie message between staff: a short voice clip or a quick call ("Table 5 food ready").
/// Kept 24 hours for replay, then deleted.
/// </summary>
public class VoiceMessage : BaseEntity
{
    public Guid SenderUserId { get; set; }

    [MaxLength(100)]
    public string SenderName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string SenderRole { get; set; } = string.Empty;

    /// <summary>All, Kitchen, Floor, Counter, Hotel, or Direct (one person: RecipientUserId).</summary>
    [MaxLength(20)]
    public string Channel { get; set; } = "All";

    public Guid? RecipientUserId { get; set; }

    [MaxLength(100)]
    public string? RecipientName { get; set; }

    /// <summary>Quick call key (e.g. "foodReady"); each device reads it aloud in its own language. Null for voice.</summary>
    [MaxLength(40)]
    public string? QuickKey { get; set; }

    /// <summary>Table number for quick calls that mention one.</summary>
    [MaxLength(20)]
    public string? QuickTable { get; set; }

    /// <summary>Recorded clip (Opus/WebM or MP4/AAC from Safari). Null for quick calls.</summary>
    public byte[]? Audio { get; set; }

    [MaxLength(50)]
    public string? AudioMime { get; set; }

    public int DurationMs { get; set; }
}
