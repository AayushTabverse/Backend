using menu_backend.DTOs;
using menu_backend.DTOs.AI;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? sentiment = null)
    {
        var result = await _reviewService.GetReviewsAsync(page, pageSize, sentiment);
        return Ok(ApiResponse<PaginatedReviewsResponse>.Ok(result));
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetReviewAnalytics()
    {
        var result = await _reviewService.GetReviewAnalyticsAsync();
        return Ok(ApiResponse<ReviewAnalyticsResponse>.Ok(result));
    }

    [HttpPost("{id}/generate-reply")]
    public async Task<IActionResult> GenerateReply(Guid id)
    {
        var result = await _reviewService.GenerateReplyAsync(id);
        return Ok(ApiResponse<GeneratedReplyResponse>.Ok(result));
    }

    [HttpPost("{id}/reply")]
    public async Task<IActionResult> PostReply(Guid id, [FromBody] PostReplyRequest request)
    {
        var result = await _reviewService.PostReplyAsync(id, request);
        return Ok(ApiResponse<GoogleReviewResponse>.Ok(result));
    }
}
