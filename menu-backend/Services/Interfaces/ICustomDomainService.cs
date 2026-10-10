using menu_backend.DTOs.Website;

namespace menu_backend.Services.Interfaces;

public interface ICustomDomainService
{
    /// <summary>Connect (or replace) the restaurant's own domain and return the setup status.</summary>
    Task<CustomDomainStatusResponse> ConnectAsync(string domain);

    /// <summary>Disconnect the restaurant's own domain.</summary>
    Task RemoveAsync();

    /// <summary>DNS records to add (checked live), setup steps and an honest overall state. Cached ~15s unless forced.</summary>
    Task<CustomDomainStatusResponse> GetStatusAsync(bool forceCheck = false);

    /// <summary>Tenant whose verified custom domain is <paramref name="host"/>, if any.</summary>
    Task<string?> ResolveTenantIdAsync(string host);
}
