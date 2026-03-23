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
}
