using menu_backend.DTOs.AI;

namespace menu_backend.Services.Interfaces;

public interface IAiContentService
{
    Task<GeneratedPostResponse> GeneratePostAsync(GeneratePostRequest request);
    Task<string> GenerateImageAsync(string prompt);
    Task<MarketingPostResponse> ApprovePostAsync(Guid postId, ApprovePostRequest request);
    Task<MarketingPostResponse> RejectPostAsync(Guid postId);
    Task<PaginatedPostsResponse> GetPostHistoryAsync(int page = 1, int pageSize = 20, string? status = null);
    Task<List<ContentCalendarResponse>> GetContentCalendarAsync(int month, int year);
    Task<List<SocialConnectionResponse>> GetSocialConnectionsAsync();
    Task DisconnectSocialAsync(string platform);
}
