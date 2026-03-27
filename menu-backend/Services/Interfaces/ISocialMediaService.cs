using menu_backend.DTOs.AI;

namespace menu_backend.Services.Interfaces;

public interface ISocialMediaService
{
    string GetFacebookAuthUrl(string tenantId);
    Task<SocialConnectionResponse> HandleFacebookCallbackAsync(string code, string tenantId);
    string GetGoogleAuthUrl(string tenantId);
    Task<SocialConnectionResponse> HandleGoogleCallbackAsync(string code, string tenantId);
    Task<SocialPostResult> PublishPostAsync(Guid postId);
}
