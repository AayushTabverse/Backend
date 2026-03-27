using System.Text;
using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.AI;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SocialController : ControllerBase
{
    private readonly ISocialMediaService _socialService;
    private readonly ITenantProvider _tenantProvider;

    public SocialController(ISocialMediaService socialService, ITenantProvider tenantProvider)
    {
        _socialService = socialService;
        _tenantProvider = tenantProvider;
    }

    /// <summary>
    /// Get the Facebook OAuth URL to start the connection flow.
    /// Admin clicks "Connect Facebook" → frontend opens this URL in a popup.
    /// </summary>
    [HttpGet("facebook/auth-url")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public IActionResult GetFacebookAuthUrl()
    {
        var tenantId = _tenantProvider.TenantId!;
        var url = _socialService.GetFacebookAuthUrl(tenantId);
        return Ok(ApiResponse<OAuthUrlResponse>.Ok(new OAuthUrlResponse { AuthUrl = url }));
    }

    /// <summary>
    /// Facebook OAuth callback — exchanged code → token → page info.
    /// Called by the frontend after the OAuth popup redirects back with ?code=...
    /// </summary>
    [HttpPost("facebook/callback")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> FacebookCallback([FromBody] OAuthCallbackRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;
        var result = await _socialService.HandleFacebookCallbackAsync(request.Code, tenantId);
        return Ok(ApiResponse<SocialConnectionResponse>.Ok(result, "Facebook connected successfully."));
    }

    /// <summary>
    /// Get the Google OAuth URL for Google Business Profile.
    /// </summary>
    [HttpGet("google/auth-url")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public IActionResult GetGoogleAuthUrl()
    {
        var tenantId = _tenantProvider.TenantId!;
        var url = _socialService.GetGoogleAuthUrl(tenantId);
        return Ok(ApiResponse<OAuthUrlResponse>.Ok(new OAuthUrlResponse { AuthUrl = url }));
    }

    /// <summary>
    /// Google OAuth callback — exchange code for token.
    /// </summary>
    [HttpPost("google/callback")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> GoogleCallback([FromBody] OAuthCallbackRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;
        var result = await _socialService.HandleGoogleCallbackAsync(request.Code, tenantId);
        return Ok(ApiResponse<SocialConnectionResponse>.Ok(result, "Google connected successfully."));
    }

    /// <summary>
    /// Publish an approved post to connected social media platforms.
    /// </summary>
    [HttpPost("publish/{postId}")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> PublishPost(Guid postId)
    {
        var result = await _socialService.PublishPostAsync(postId);
        if (result.Success)
            return Ok(ApiResponse<SocialPostResult>.Ok(result, "Post published successfully."));
        return Ok(ApiResponse<SocialPostResult>.Fail(result.Error ?? "Publishing failed."));
    }
}
