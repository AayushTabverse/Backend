namespace menu_backend.DTOs.AI;

// ── Request DTOs ──

public class GeneratePostRequest
{
    public string ContentType { get; set; } = "social"; // social, festival, menu-highlight, testimonial, weekly-special
    public string Platform { get; set; } = "both"; // instagram, facebook, both
    public string? CustomPrompt { get; set; }
}

public class GenerateImageRequest
{
    public string Prompt { get; set; } = string.Empty;
}

public class ApprovePostRequest
{
    public string? EditedText { get; set; }
    public string? EditedCaption { get; set; }
    public DateTime? ScheduledAt { get; set; }
}

public class GenerateReplyRequest
{
    public string ReviewText { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string AuthorName { get; set; } = string.Empty;
}

// ── Response DTOs ──

public class GeneratedPostResponse
{
    public Guid Id { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = new();
    public string? ImageUrl { get; set; }
    public string SuggestedCaption { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
}

public class MarketingPostResponse
{
    public Guid Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentText { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = new();
    public string? ImageUrl { get; set; }
    public string? SuggestedCaption { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FailureReason { get; set; }
}

public class ContentCalendarResponse
{
    public DateTime Date { get; set; }
    public List<MarketingPostResponse> Posts { get; set; } = new();
}

public class PaginatedPostsResponse
{
    public List<MarketingPostResponse> Posts { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class SocialConnectionResponse
{
    public string Platform { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? PageName { get; set; }
    public string? PageId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

// ── Google Reviews DTOs ──

public class GoogleReviewResponse
{
    public Guid Id { get; set; }
    public string GoogleReviewId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime ReviewCreateTime { get; set; }
    public string? ReplyText { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string? Sentiment { get; set; }
    public List<string> SentimentThemes { get; set; } = new();
    public string? AuthorProfileUrl { get; set; }
}

public class ReviewAnalyticsResponse
{
    public double AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public SentimentBreakdown Sentiment { get; set; } = new();
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // star → count
    public List<string> CommonThemes { get; set; } = new();
    public List<ReviewTrend> Trend { get; set; } = new();
}

public class SentimentBreakdown
{
    public int Positive { get; set; }
    public int Neutral { get; set; }
    public int Negative { get; set; }
}

public class ReviewTrend
{
    public string Period { get; set; } = string.Empty; // month label
    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }
}

public class PostReplyRequest
{
    public string ReplyText { get; set; } = string.Empty;
}

public class PaginatedReviewsResponse
{
    public List<GoogleReviewResponse> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class GeneratedReplyResponse
{
    public string ReplyText { get; set; } = string.Empty;
}

// ── Social OAuth DTOs ──

public class OAuthUrlResponse
{
    public string AuthUrl { get; set; } = string.Empty;
}

public class OAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
}

public class SocialPostRequest
{
    public Guid PostId { get; set; }
}

public class SocialPostResult
{
    public bool Success { get; set; }
    public string? FacebookPostId { get; set; }
    public string? InstagramPostId { get; set; }
    public string? Error { get; set; }
}
