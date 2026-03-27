using menu_backend.DTOs.AI;

namespace menu_backend.Services.Interfaces;

public interface IReviewService
{
    Task<PaginatedReviewsResponse> GetReviewsAsync(int page = 1, int pageSize = 20, string? sentiment = null);
    Task<ReviewAnalyticsResponse> GetReviewAnalyticsAsync();
    Task<GeneratedReplyResponse> GenerateReplyAsync(Guid reviewId);
    Task<GoogleReviewResponse> PostReplyAsync(Guid reviewId, PostReplyRequest request);
}
