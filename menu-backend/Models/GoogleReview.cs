using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

public class GoogleReview : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string GoogleReviewId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AuthorName { get; set; } = string.Empty;

    public int Rating { get; set; } // 1-5

    [Column(TypeName = "text")]
    public string? ReviewText { get; set; }

    public DateTime ReviewCreateTime { get; set; }

    [Column(TypeName = "text")]
    public string? ReplyText { get; set; }

    public DateTime? RepliedAt { get; set; }

    [MaxLength(20)]
    public string? Sentiment { get; set; } // Positive, Neutral, Negative

    [Column(TypeName = "text")]
    public string? SentimentThemesJson { get; set; } // JSON array of themes

    [MaxLength(200)]
    public string? AuthorProfileUrl { get; set; }
}
