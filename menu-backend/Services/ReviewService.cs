using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using menu_backend.Data;
using menu_backend.DTOs.AI;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public ReviewService(AppDbContext db, ITenantProvider tenantProvider, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<PaginatedReviewsResponse> GetReviewsAsync(int page = 1, int pageSize = 20, string? sentiment = null)
    {
        var tenantId = _tenantProvider.TenantId!;
        var query = _db.GoogleReviews.Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(sentiment))
            query = query.Where(r => r.Sentiment == sentiment);

        var totalCount = await query.CountAsync();
        var reviews = await query
            .OrderByDescending(r => r.ReviewCreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedReviewsResponse
        {
            Reviews = reviews.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<ReviewAnalyticsResponse> GetReviewAnalyticsAsync()
    {
        var tenantId = _tenantProvider.TenantId!;
        var reviews = await _db.GoogleReviews
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        if (!reviews.Any())
        {
            return new ReviewAnalyticsResponse
            {
                AvgRating = 0,
                TotalReviews = 0,
                Sentiment = new SentimentBreakdown(),
                RatingDistribution = Enumerable.Range(1, 5).ToDictionary(i => i, _ => 0),
                CommonThemes = new List<string>(),
                Trend = new List<ReviewTrend>()
            };
        }

        var avgRating = reviews.Average(r => r.Rating);
        var ratingDist = Enumerable.Range(1, 5)
            .ToDictionary(i => i, i => reviews.Count(r => r.Rating == i));

        var sentimentBreakdown = new SentimentBreakdown
        {
            Positive = reviews.Count(r => r.Sentiment == "Positive"),
            Neutral = reviews.Count(r => r.Sentiment == "Neutral"),
            Negative = reviews.Count(r => r.Sentiment == "Negative")
        };

        // Extract common themes from all reviews
        var allThemes = reviews
            .Where(r => !string.IsNullOrWhiteSpace(r.SentimentThemesJson))
            .SelectMany(r =>
            {
                try { return JsonSerializer.Deserialize<List<string>>(r.SentimentThemesJson!) ?? new(); }
                catch { return new List<string>(); }
            })
            .GroupBy(t => t.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        // Monthly trend (last 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var trend = reviews
            .Where(r => r.ReviewCreateTime >= sixMonthsAgo)
            .GroupBy(r => new { r.ReviewCreateTime.Year, r.ReviewCreateTime.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new ReviewTrend
            {
                Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                AvgRating = Math.Round(g.Average(r => r.Rating), 1),
                ReviewCount = g.Count()
            })
            .ToList();

        return new ReviewAnalyticsResponse
        {
            AvgRating = Math.Round(avgRating, 1),
            TotalReviews = reviews.Count,
            Sentiment = sentimentBreakdown,
            RatingDistribution = ratingDist,
            CommonThemes = allThemes,
            Trend = trend
        };
    }

    public async Task<GeneratedReplyResponse> GenerateReplyAsync(Guid reviewId)
    {
        var tenantId = _tenantProvider.TenantId!;
        var review = await _db.GoogleReviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.TenantId == tenantId)
            ?? throw new Exception("Review not found.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new Exception("Tenant not found.");

        var prompt = BuildReplyPrompt(review, tenant.Name);
        var aiResponse = await CallOpenAiChatAsync(prompt);

        // Parse reply text
        var replyText = aiResponse.Trim();
        if (replyText.StartsWith("\"") && replyText.EndsWith("\""))
            replyText = replyText[1..^1];

        return new GeneratedReplyResponse { ReplyText = replyText };
    }

    public async Task<GoogleReviewResponse> PostReplyAsync(Guid reviewId, PostReplyRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;
        var review = await _db.GoogleReviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.TenantId == tenantId)
            ?? throw new Exception("Review not found.");

        review.ReplyText = request.ReplyText;
        review.RepliedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // TODO: When Google Business Profile API is connected, also post the reply to Google
        // via the API: accounts/{accountId}/locations/{locationId}/reviews/{reviewId}/reply

        return MapToResponse(review);
    }

    // ── Private Helpers ──

    private string BuildReplyPrompt(GoogleReview review, string restaurantName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are the owner of \"{restaurantName}\" restaurant. Generate a professional, warm reply to this Google review.");
        sb.AppendLine();
        sb.AppendLine($"Customer name: {review.AuthorName}");
        sb.AppendLine($"Rating: {review.Rating}/5 stars");
        sb.AppendLine($"Review: {review.ReviewText}");
        sb.AppendLine();

        if (review.Rating >= 4)
            sb.AppendLine("This is a positive review. Thank the customer warmly, mention something specific from their review, and invite them back.");
        else if (review.Rating == 3)
            sb.AppendLine("This is a mixed review. Acknowledge the feedback, thank them for visiting, and mention how you'll address concerns.");
        else
            sb.AppendLine("This is a negative review. Apologize sincerely, address the concern, offer to make it right, and provide contact info for follow-up.");

        sb.AppendLine();
        sb.AppendLine("Keep the reply under 100 words. Be genuine, not generic. Do NOT use markdown. Respond with ONLY the reply text, no JSON.");

        return sb.ToString();
    }

    private async Task<string> CallOpenAiChatAsync(string prompt)
    {
        var apiKey = _config["OpenAI:ApiKey"]
            ?? throw new Exception("OpenAI API key not configured.");

        var requestBody = new
        {
            model = _config["OpenAI:Model"] ?? "gpt-4o",
            messages = new[]
            {
                new { role = "system", content = "You are a professional restaurant owner responding to customer reviews. Be genuine and helpful." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 300
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(httpRequest);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"OpenAI API error: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? throw new Exception("Empty response from OpenAI.");
    }

    private static GoogleReviewResponse MapToResponse(GoogleReview review)
    {
        var themes = new List<string>();
        if (!string.IsNullOrWhiteSpace(review.SentimentThemesJson))
        {
            try { themes = JsonSerializer.Deserialize<List<string>>(review.SentimentThemesJson) ?? new(); } catch { }
        }

        return new GoogleReviewResponse
        {
            Id = review.Id,
            GoogleReviewId = review.GoogleReviewId,
            AuthorName = review.AuthorName,
            Rating = review.Rating,
            ReviewText = review.ReviewText,
            ReviewCreateTime = review.ReviewCreateTime,
            ReplyText = review.ReplyText,
            RepliedAt = review.RepliedAt,
            Sentiment = review.Sentiment,
            SentimentThemes = themes,
            AuthorProfileUrl = review.AuthorProfileUrl
        };
    }
}
