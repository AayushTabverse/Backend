using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using menu_backend.Data;
using menu_backend.DTOs.AI;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class AiContentService : IAiContentService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiContentService(AppDbContext db, ITenantProvider tenantProvider, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<GeneratedPostResponse> GeneratePostAsync(GeneratePostRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;

        // Gather restaurant context for the AI prompt
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new Exception("Tenant not found.");

        var topItems = await _db.OrderItems
            .Include(oi => oi.MenuItem)
            .Where(oi => oi.TenantId == tenantId && oi.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .GroupBy(oi => oi.MenuItem!.Name)
            .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        var categories = await _db.MenuCategories
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => c.Name)
            .ToListAsync();

        var prompt = BuildPrompt(request, tenant.Name, topItems.Select(t => $"{t.Name} ({t.Count} sold)").ToList(), categories, request.CustomPrompt);
        var aiResponse = await CallOpenAiChatAsync(prompt);

        // Parse structured response
        var parsed = ParseAiPostResponse(aiResponse);

        // Generate image if applicable
        string? imageUrl = null;
        if (request.ContentType is "social" or "menu-highlight" or "festival")
        {
            try
            {
                var imagePrompt = $"Professional restaurant marketing photo for {tenant.Name}: {parsed.caption}. Food photography style, warm lighting, appetizing, no text overlay.";
                imageUrl = await GenerateImageAsync(imagePrompt);
            }
            catch
            {
                // Image generation is optional; continue without it
            }
        }

        var post = new MarketingPost
        {
            TenantId = tenantId,
            Platform = request.Platform,
            ContentType = request.ContentType,
            ContentText = parsed.text,
            HashtagsJson = JsonSerializer.Serialize(parsed.hashtags),
            ImageUrl = imageUrl,
            SuggestedCaption = parsed.caption,
            Status = "Draft",
            CustomPrompt = request.CustomPrompt
        };

        _db.MarketingPosts.Add(post);
        await _db.SaveChangesAsync();

        var response = MapToResponse(post);
        return new GeneratedPostResponse
        {
            Id = response.Id,
            ContentText = response.ContentText,
            Hashtags = response.Hashtags,
            ImageUrl = response.ImageUrl,
            SuggestedCaption = response.SuggestedCaption ?? "",
            Platform = response.Platform,
            ContentType = response.ContentType,
            Status = response.Status,
            CreatedAt = response.CreatedAt
        };
    }

    public async Task<string> GenerateImageAsync(string prompt)
    {
        var apiKey = _config["OpenAI:ApiKey"]
            ?? throw new Exception("OpenAI API key not configured.");

        var requestBody = new
        {
            model = "dall-e-3",
            prompt = prompt,
            n = 1,
            size = "1024x1024",
            quality = "standard"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(httpRequest);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"DALL-E API error: {json}");

        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
        return url ?? throw new Exception("No image URL returned.");
    }

    public async Task<MarketingPostResponse> ApprovePostAsync(Guid postId, ApprovePostRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;
        var post = await _db.MarketingPosts.FirstOrDefaultAsync(p => p.Id == postId && p.TenantId == tenantId)
            ?? throw new Exception("Post not found.");

        if (!string.IsNullOrWhiteSpace(request.EditedText))
            post.ContentText = request.EditedText;

        if (!string.IsNullOrWhiteSpace(request.EditedCaption))
            post.SuggestedCaption = request.EditedCaption;

        post.Status = request.ScheduledAt.HasValue ? "Scheduled" : "Approved";
        post.ScheduledAt = request.ScheduledAt;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToResponse(post);
    }

    public async Task<MarketingPostResponse> RejectPostAsync(Guid postId)
    {
        var tenantId = _tenantProvider.TenantId!;
        var post = await _db.MarketingPosts.FirstOrDefaultAsync(p => p.Id == postId && p.TenantId == tenantId)
            ?? throw new Exception("Post not found.");

        post.Status = "Rejected";
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToResponse(post);
    }

    public async Task<PaginatedPostsResponse> GetPostHistoryAsync(int page = 1, int pageSize = 20, string? status = null)
    {
        var tenantId = _tenantProvider.TenantId!;
        var query = _db.MarketingPosts.Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        var totalCount = await query.CountAsync();
        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedPostsResponse
        {
            Posts = posts.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<List<ContentCalendarResponse>> GetContentCalendarAsync(int month, int year)
    {
        var tenantId = _tenantProvider.TenantId!;
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        var posts = await _db.MarketingPosts
            .Where(p => p.TenantId == tenantId && p.CreatedAt >= startDate && p.CreatedAt < endDate)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        return posts
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new ContentCalendarResponse
            {
                Date = g.Key,
                Posts = g.Select(MapToResponse).ToList()
            })
            .OrderBy(c => c.Date)
            .ToList();
    }

    public async Task<List<SocialConnectionResponse>> GetSocialConnectionsAsync()
    {
        var tenantId = _tenantProvider.TenantId!;
        var connections = await _db.SocialMediaConnections
            .Where(c => c.TenantId == tenantId)
            .ToListAsync();

        // Return all platforms, even if not connected
        var platforms = new[] { "facebook", "instagram", "google" };
        return platforms.Select(platform =>
        {
            var conn = connections.FirstOrDefault(c => c.Platform == platform);
            return new SocialConnectionResponse
            {
                Platform = platform,
                IsConnected = conn?.IsConnected ?? false,
                PageName = conn?.PageName,
                PageId = conn?.PageId,
                ExpiresAt = conn?.ExpiresAt
            };
        }).ToList();
    }

    public async Task DisconnectSocialAsync(string platform)
    {
        var tenantId = _tenantProvider.TenantId!;
        var conn = await _db.SocialMediaConnections
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Platform == platform);

        if (conn != null)
        {
            conn.IsConnected = false;
            conn.AccessToken = null;
            conn.RefreshToken = null;
            conn.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ── Private Helpers ──

    private string BuildPrompt(GeneratePostRequest request, string restaurantName, List<string> topItems, List<string> categories, string? customPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are a social media marketing expert for restaurants. Generate a marketing post for \"{restaurantName}\".");
        sb.AppendLine();
        sb.AppendLine($"Platform: {request.Platform}");
        sb.AppendLine($"Content type: {request.ContentType}");

        if (topItems.Any())
            sb.AppendLine($"Top selling items (last 30 days): {string.Join(", ", topItems)}");

        if (categories.Any())
            sb.AppendLine($"Menu categories: {string.Join(", ", categories)}");

        if (!string.IsNullOrWhiteSpace(customPrompt))
            sb.AppendLine($"Additional instructions: {customPrompt}");

        sb.AppendLine();

        switch (request.ContentType)
        {
            case "festival":
                sb.AppendLine("Generate a festive/seasonal promotional post. Reference upcoming Indian festivals or seasons.");
                break;
            case "menu-highlight":
                sb.AppendLine("Highlight one of the top-selling menu items with a mouth-watering description.");
                break;
            case "testimonial":
                sb.AppendLine("Create a post showcasing customer love/appreciation. Use a warm, authentic tone.");
                break;
            case "weekly-special":
                sb.AppendLine("Create a weekly special offer or combo deal post that drives urgency.");
                break;
            default:
                sb.AppendLine("Create an engaging social media post that is inviting and builds brand awareness.");
                break;
        }

        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Respond ONLY with valid JSON in this exact format:");
        sb.AppendLine("{");
        sb.AppendLine("  \"text\": \"The post text (2-3 sentences, engaging, with emojis)\",");
        sb.AppendLine("  \"hashtags\": [\"#hashtag1\", \"#hashtag2\", ...],");
        sb.AppendLine("  \"caption\": \"A short suggested caption for the image (1 sentence)\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private async Task<string> CallOpenAiChatAsync(string prompt)
    {
        var apiKey = _config["OpenAI:ApiKey"]
            ?? throw new Exception("OpenAI API key not configured. Add 'OpenAI:ApiKey' to appsettings.json.");

        var requestBody = new
        {
            model = _config["OpenAI:Model"] ?? "gpt-4o",
            messages = new[]
            {
                new { role = "system", content = "You are a professional restaurant marketing assistant. Always respond with valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = 0.8,
            max_tokens = 500
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
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? throw new Exception("Empty response from OpenAI.");
    }

    private static (string text, List<string> hashtags, string caption) ParseAiPostResponse(string aiResponse)
    {
        // Strip markdown code fences if present
        var cleaned = aiResponse.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline > 0)
                cleaned = cleaned[(firstNewline + 1)..];
            if (cleaned.EndsWith("```"))
                cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var text = root.GetProperty("text").GetString() ?? "";
            var caption = root.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";

            var hashtags = new List<string>();
            if (root.TryGetProperty("hashtags", out var hashArray))
            {
                foreach (var tag in hashArray.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (!string.IsNullOrWhiteSpace(tagStr))
                        hashtags.Add(tagStr);
                }
            }

            return (text, hashtags, caption);
        }
        catch
        {
            // Fallback: use the raw response as text
            return (cleaned, new List<string>(), "");
        }
    }

    private static MarketingPostResponse MapToResponse(MarketingPost post)
    {
        var hashtags = new List<string>();
        if (!string.IsNullOrWhiteSpace(post.HashtagsJson))
        {
            try { hashtags = JsonSerializer.Deserialize<List<string>>(post.HashtagsJson) ?? new(); } catch { }
        }

        return new MarketingPostResponse
        {
            Id = post.Id,
            Platform = post.Platform,
            ContentType = post.ContentType,
            ContentText = post.ContentText,
            Hashtags = hashtags,
            ImageUrl = post.ImageUrl,
            SuggestedCaption = post.SuggestedCaption,
            Status = post.Status,
            ScheduledAt = post.ScheduledAt,
            PostedAt = post.PostedAt,
            CreatedAt = post.CreatedAt,
            FailureReason = post.FailureReason
        };
    }
}
