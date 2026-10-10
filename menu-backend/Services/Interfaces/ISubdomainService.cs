using menu_backend.DTOs.Website;

namespace menu_backend.Services.Interfaces;

public interface ISubdomainService
{
    /// <summary>Generate subdomain suggestions based on restaurant name.</summary>
    Task<SubdomainSuggestionsResponse> GetSuggestionsAsync();

    /// <summary>Check if a subdomain is available.</summary>
    Task<CheckSubdomainResponse> CheckAvailabilityAsync(string subdomain);

    /// <summary>Claim a subdomain for the current tenant and create DNS record via Hostinger.</summary>
    Task<SubdomainResponse> ClaimSubdomainAsync(string subdomain);

    /// <summary>Release the current tenant's subdomain and remove DNS record.</summary>
    Task<SubdomainResponse> ReleaseSubdomainAsync();

    /// <summary>Get the current tenant's subdomain info.</summary>
    Task<SubdomainResponse> GetCurrentSubdomainAsync();

    /// <summary>Resolve a subdomain to a tenantId (public, for subdomain-based routing).</summary>
    Task<string?> ResolveTenantIdAsync(string subdomain);

    /// <summary>Claim the subdomain (setting up DNS if needed) and publish the website in one step.</summary>
    Task<WebsiteStatusResponse> PublishAsync(string subdomain);

    /// <summary>Re-run the DNS step for the current subdomain.</summary>
    Task<WebsiteStatusResponse> RetryDnsAsync();

    /// <summary>
    /// Owner-facing website status from facts: stored DNS outcome, whether the address resolves and
    /// whether the site responds. Cached ~15s unless <paramref name="forceCheck"/>.
    /// </summary>
    Task<WebsiteStatusResponse> GetStatusAsync(bool forceCheck = false);
}
