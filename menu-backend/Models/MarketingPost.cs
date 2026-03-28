using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

public class MarketingPost : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string Platform { get; set; } = "both"; // instagram, facebook, both

    [Required]
    [MaxLength(30)]
    public string ContentType { get; set; } = "social"; // social, festival, menu-highlight, testimonial, weekly-special

    [Column(TypeName = "text")]
    public string ContentText { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? HashtagsJson { get; set; } // JSON array of hashtags

    [Column(TypeName = "text")]
    public string? ImageUrl { get; set; }

    [Column(TypeName = "text")]
    public string? SuggestedCaption { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Scheduled, Posted, Rejected, Failed

    public DateTime? ScheduledAt { get; set; }
    public DateTime? PostedAt { get; set; }

    [MaxLength(500)]
    public string? CustomPrompt { get; set; }

    [MaxLength(500)]
    public string? FacebookPostId { get; set; }

    [MaxLength(500)]
    public string? InstagramPostId { get; set; }

    [Column(TypeName = "text")]
    public string? FailureReason { get; set; }
}
