using menu_backend.DTOs;
using menu_backend.DTOs.AI;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class AiController : ControllerBase
{
    private readonly IAiContentService _aiService;

    public AiController(IAiContentService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("generate-post")]
    public async Task<IActionResult> GeneratePost([FromBody] GeneratePostRequest request)
    {
        var result = await _aiService.GeneratePostAsync(request);
        return Ok(ApiResponse<GeneratedPostResponse>.Ok(result));
    }

    [HttpPost("generate-image")]
    public async Task<IActionResult> GenerateImage([FromBody] GenerateImageRequest request)
    {
        var imageUrl = await _aiService.GenerateImageAsync(request.Prompt);
        return Ok(ApiResponse<string>.Ok(imageUrl));
    }

    [HttpPut("post/{id}/approve")]
    public async Task<IActionResult> ApprovePost(Guid id, [FromBody] ApprovePostRequest request)
    {
        var result = await _aiService.ApprovePostAsync(id, request);
        return Ok(ApiResponse<MarketingPostResponse>.Ok(result));
    }

    [HttpPut("post/{id}/reject")]
    public async Task<IActionResult> RejectPost(Guid id)
    {
        var result = await _aiService.RejectPostAsync(id);
        return Ok(ApiResponse<MarketingPostResponse>.Ok(result));
    }

    [HttpGet("post/history")]
    public async Task<IActionResult> GetPostHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        var result = await _aiService.GetPostHistoryAsync(page, pageSize, status);
        return Ok(ApiResponse<PaginatedPostsResponse>.Ok(result));
    }

    [HttpGet("content-calendar")]
    public async Task<IActionResult> GetContentCalendar([FromQuery] int month, [FromQuery] int year)
    {
        var result = await _aiService.GetContentCalendarAsync(month, year);
        return Ok(ApiResponse<List<ContentCalendarResponse>>.Ok(result));
    }

    [HttpGet("social-connections")]
    public async Task<IActionResult> GetSocialConnections()
    {
        var result = await _aiService.GetSocialConnectionsAsync();
        return Ok(ApiResponse<List<SocialConnectionResponse>>.Ok(result));
    }

    [HttpDelete("social/{platform}")]
    public async Task<IActionResult> DisconnectSocial(string platform)
    {
        await _aiService.DisconnectSocialAsync(platform);
        return Ok(ApiResponse.Ok("Disconnected successfully."));
    }
}
