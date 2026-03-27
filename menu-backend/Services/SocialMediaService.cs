using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using menu_backend.Data;
using menu_backend.DTOs.AI;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class SocialMediaService : ISocialMediaService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public SocialMediaService(AppDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
    }

    // ── Facebook / Instagram OAuth ──

    public string GetFacebookAuthUrl(string tenantId)
    {
        var appId = _config["Facebook:AppId"]
            ?? throw new Exception("Facebook:AppId not configured.");
        var redirectUri = _config["Facebook:RedirectUri"]
            ?? throw new Exception("Facebook:RedirectUri not configured.");

        // State parameter carries tenantId so callback knows which tenant
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(tenantId));

        // Request pages_manage_posts + instagram_basic + instagram_content_publish
        var scopes = "pages_show_list,pages_read_engagement,pages_manage_posts,instagram_basic,instagram_content_publish";

        return $"https://www.facebook.com/v21.0/dialog/oauth" +
               $"?client_id={appId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={scopes}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code";
    }

    public async Task<SocialConnectionResponse> HandleFacebookCallbackAsync(string code, string tenantId)
    {
        var appId = _config["Facebook:AppId"]!;
        var appSecret = _config["Facebook:AppSecret"]!;
        var redirectUri = _config["Facebook:RedirectUri"]!;

        // 1. Exchange code for short-lived user token
        var tokenUrl = $"https://graph.facebook.com/v21.0/oauth/access_token" +
                       $"?client_id={appId}" +
                       $"&client_secret={appSecret}" +
                       $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                       $"&code={code}";

        var tokenResponse = await _httpClient.GetStringAsync(tokenUrl);
        using var tokenDoc = JsonDocument.Parse(tokenResponse);
        var shortLivedToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;

        // 2. Exchange for long-lived token (60 days)
        var longTokenUrl = $"https://graph.facebook.com/v21.0/oauth/access_token" +
                           $"?grant_type=fb_exchange_token" +
                           $"&client_id={appId}" +
                           $"&client_secret={appSecret}" +
                           $"&fb_exchange_token={shortLivedToken}";

        var longTokenResponse = await _httpClient.GetStringAsync(longTokenUrl);
        using var longTokenDoc = JsonDocument.Parse(longTokenResponse);
        var longLivedToken = longTokenDoc.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = longTokenDoc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : 5184000;

        // 3. Get user's pages (need page access token for posting)
        var pagesUrl = $"https://graph.facebook.com/v21.0/me/accounts?access_token={longLivedToken}";
        var pagesResponse = await _httpClient.GetStringAsync(pagesUrl);
        using var pagesDoc = JsonDocument.Parse(pagesResponse);
        var pages = pagesDoc.RootElement.GetProperty("data");

        if (pages.GetArrayLength() == 0)
            throw new Exception("No Facebook Pages found. Please create a Facebook Page first.");

        // Use the first page (most restaurants have one page)
        var page = pages[0];
        var pageAccessToken = page.GetProperty("access_token").GetString()!;
        var pageId = page.GetProperty("id").GetString()!;
        var pageName = page.GetProperty("name").GetString()!;

        // 4. Save Facebook connection
        var fbConn = await _db.SocialMediaConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Platform == "facebook");

        if (fbConn == null)
        {
            fbConn = new SocialMediaConnection { TenantId = tenantId, Platform = "facebook" };
            _db.SocialMediaConnections.Add(fbConn);
        }

        fbConn.AccessToken = pageAccessToken;
        fbConn.RefreshToken = longLivedToken;
        fbConn.PageId = pageId;
        fbConn.PageName = pageName;
        fbConn.IsConnected = true;
        fbConn.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        fbConn.IsDeleted = false;
        fbConn.UpdatedAt = DateTime.UtcNow;

        // 5. Try to get Instagram Business Account linked to this page
        try
        {
            var igUrl = $"https://graph.facebook.com/v21.0/{pageId}?fields=instagram_business_account&access_token={pageAccessToken}";
            var igResponse = await _httpClient.GetStringAsync(igUrl);
            using var igDoc = JsonDocument.Parse(igResponse);

            if (igDoc.RootElement.TryGetProperty("instagram_business_account", out var igAccount))
            {
                var igId = igAccount.GetProperty("id").GetString()!;

                // Get IG username
                var igInfoUrl = $"https://graph.facebook.com/v21.0/{igId}?fields=username&access_token={pageAccessToken}";
                var igInfoResponse = await _httpClient.GetStringAsync(igInfoUrl);
                using var igInfoDoc = JsonDocument.Parse(igInfoResponse);
                var igUsername = igInfoDoc.RootElement.TryGetProperty("username", out var un) ? un.GetString() : igId;

                var igConn = await _db.SocialMediaConnections
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Platform == "instagram");

                if (igConn == null)
                {
                    igConn = new SocialMediaConnection { TenantId = tenantId, Platform = "instagram" };
                    _db.SocialMediaConnections.Add(igConn);
                }

                igConn.AccessToken = pageAccessToken; // IG uses page token
                igConn.PageId = igId;
                igConn.PageName = $"@{igUsername}";
                igConn.IsConnected = true;
                igConn.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                igConn.IsDeleted = false;
                igConn.UpdatedAt = DateTime.UtcNow;
            }
        }
        catch { /* Instagram not linked — skip silently */ }

        await _db.SaveChangesAsync();

        return new SocialConnectionResponse
        {
            Platform = "facebook",
            IsConnected = true,
            PageName = pageName,
            PageId = pageId,
            ExpiresAt = fbConn.ExpiresAt
        };
    }

    // ── Google OAuth ──

    public string GetGoogleAuthUrl(string tenantId)
    {
        var clientId = _config["Google:ClientId"]
            ?? throw new Exception("Google:ClientId not configured.");
        var redirectUri = _config["Google:RedirectUri"]
            ?? throw new Exception("Google:RedirectUri not configured.");

        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(tenantId));
        var scopes = "https://www.googleapis.com/auth/business.manage";

        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code" +
               $"&access_type=offline" +
               $"&prompt=consent";
    }

    public async Task<SocialConnectionResponse> HandleGoogleCallbackAsync(string code, string tenantId)
    {
        var clientId = _config["Google:ClientId"]!;
        var clientSecret = _config["Google:ClientSecret"]!;
        var redirectUri = _config["Google:RedirectUri"]!;

        // Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var tokenResponse = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new Exception($"Google token exchange failed: {tokenJson}");

        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : 3600;

        // Get account name
        var accountName = "Google Business";
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://mybusinessaccountmanagement.googleapis.com/v1/accounts");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var accountResponse = await _httpClient.SendAsync(req);
            var accountJson = await accountResponse.Content.ReadAsStringAsync();
            using var accountDoc = JsonDocument.Parse(accountJson);
            if (accountDoc.RootElement.TryGetProperty("accounts", out var accounts) && accounts.GetArrayLength() > 0)
            {
                accountName = accounts[0].TryGetProperty("accountName", out var an)
                    ? an.GetString() ?? "Google Business"
                    : "Google Business";
            }
        }
        catch { /* Use default name */ }

        // Save Google connection
        var conn = await _db.SocialMediaConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Platform == "google");

        if (conn == null)
        {
            conn = new SocialMediaConnection { TenantId = tenantId, Platform = "google" };
            _db.SocialMediaConnections.Add(conn);
        }

        conn.AccessToken = accessToken;
        conn.RefreshToken = refreshToken;
        conn.PageName = accountName;
        conn.IsConnected = true;
        conn.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        conn.IsDeleted = false;
        conn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new SocialConnectionResponse
        {
            Platform = "google",
            IsConnected = true,
            PageName = accountName,
            ExpiresAt = conn.ExpiresAt
        };
    }

    // ── Publish Post to Facebook / Instagram ──

    public async Task<SocialPostResult> PublishPostAsync(Guid postId)
    {
        var post = await _db.MarketingPosts.FindAsync(postId)
            ?? throw new Exception("Post not found.");

        var result = new SocialPostResult { Success = true };

        // Post to Facebook
        if (post.Platform is "facebook" or "both")
        {
            try
            {
                var fbConn = await _db.SocialMediaConnections
                    .FirstOrDefaultAsync(c => c.TenantId == post.TenantId && c.Platform == "facebook" && c.IsConnected);

                if (fbConn != null)
                {
                    var fullText = post.ContentText;
                    if (!string.IsNullOrWhiteSpace(post.HashtagsJson))
                    {
                        var hashtags = JsonSerializer.Deserialize<List<string>>(post.HashtagsJson);
                        if (hashtags?.Any() == true)
                            fullText += "\n\n" + string.Join(" ", hashtags);
                    }

                    string fbPostId;
                    if (!string.IsNullOrWhiteSpace(post.ImageUrl))
                    {
                        // Photo post
                        var url = $"https://graph.facebook.com/v21.0/{fbConn.PageId}/photos";
                        var content = new MultipartFormDataContent
                        {
                            { new StringContent(fullText), "message" },
                            { new StringContent(post.ImageUrl), "url" },
                            { new StringContent(fbConn.AccessToken!), "access_token" }
                        };
                        var response = await _httpClient.PostAsync(url, content);
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        fbPostId = doc.RootElement.TryGetProperty("post_id", out var pid) ? pid.GetString()! : doc.RootElement.GetProperty("id").GetString()!;
                    }
                    else
                    {
                        // Text-only post
                        var url = $"https://graph.facebook.com/v21.0/{fbConn.PageId}/feed";
                        var content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["message"] = fullText,
                            ["access_token"] = fbConn.AccessToken!
                        });
                        var response = await _httpClient.PostAsync(url, content);
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        fbPostId = doc.RootElement.GetProperty("id").GetString()!;
                    }

                    post.FacebookPostId = fbPostId;
                    result.FacebookPostId = fbPostId;
                }
            }
            catch (Exception ex)
            {
                result.Error = $"Facebook: {ex.Message}";
                result.Success = false;
            }
        }

        // Post to Instagram
        if (post.Platform is "instagram" or "both")
        {
            try
            {
                var igConn = await _db.SocialMediaConnections
                    .FirstOrDefaultAsync(c => c.TenantId == post.TenantId && c.Platform == "instagram" && c.IsConnected);

                if (igConn != null && !string.IsNullOrWhiteSpace(post.ImageUrl))
                {
                    var fullCaption = post.ContentText;
                    if (!string.IsNullOrWhiteSpace(post.HashtagsJson))
                    {
                        var hashtags = JsonSerializer.Deserialize<List<string>>(post.HashtagsJson);
                        if (hashtags?.Any() == true)
                            fullCaption += "\n\n" + string.Join(" ", hashtags);
                    }

                    // Step 1: Create media container
                    var createUrl = $"https://graph.facebook.com/v21.0/{igConn.PageId}/media";
                    var createContent = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["image_url"] = post.ImageUrl,
                        ["caption"] = fullCaption,
                        ["access_token"] = igConn.AccessToken!
                    });
                    var createResponse = await _httpClient.PostAsync(createUrl, createContent);
                    var createJson = await createResponse.Content.ReadAsStringAsync();
                    using var createDoc = JsonDocument.Parse(createJson);
                    var containerId = createDoc.RootElement.GetProperty("id").GetString()!;

                    // Step 2: Publish
                    var publishUrl = $"https://graph.facebook.com/v21.0/{igConn.PageId}/media_publish";
                    var publishContent = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["creation_id"] = containerId,
                        ["access_token"] = igConn.AccessToken!
                    });
                    var publishResponse = await _httpClient.PostAsync(publishUrl, publishContent);
                    var publishJson = await publishResponse.Content.ReadAsStringAsync();
                    using var publishDoc = JsonDocument.Parse(publishJson);
                    var igPostId = publishDoc.RootElement.GetProperty("id").GetString()!;

                    post.InstagramPostId = igPostId;
                    result.InstagramPostId = igPostId;
                }
                else if (igConn != null && string.IsNullOrWhiteSpace(post.ImageUrl))
                {
                    result.Error = (result.Error ?? "") + " Instagram requires an image.";
                }
            }
            catch (Exception ex)
            {
                var igError = $"Instagram: {ex.Message}";
                result.Error = string.IsNullOrEmpty(result.Error) ? igError : $"{result.Error}; {igError}";
                result.Success = false;
            }
        }

        // Update post status
        if (result.Success)
        {
            post.Status = "Posted";
            post.PostedAt = DateTime.UtcNow;
        }
        else
        {
            post.Status = "Failed";
            post.FailureReason = result.Error;
        }
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return result;
    }
}
