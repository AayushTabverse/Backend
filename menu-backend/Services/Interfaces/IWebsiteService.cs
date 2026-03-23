using menu_backend.DTOs.Website;

namespace menu_backend.Services.Interfaces;

public interface IWebsiteService
{
    /// <summary>Get website content for the current tenant (admin).</summary>
    Task<WebsiteContentResponse> GetWebsiteContentAsync();

    /// <summary>Get website content by tenantId (public).</summary>
    Task<WebsiteContentResponse> GetWebsiteContentByTenantIdAsync(string tenantId);

    /// <summary>Update website content for the current tenant.</summary>
    Task<WebsiteContentResponse> UpdateWebsiteContentAsync(UpdateWebsiteContentRequest request);
}
