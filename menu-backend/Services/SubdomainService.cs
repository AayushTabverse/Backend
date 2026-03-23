using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using menu_backend.Data;
using menu_backend.DTOs.Website;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class SubdomainService : ISubdomainService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SubdomainService> _logger;

    private const string BaseDomain = "tabverse.in";

    // Reserved subdomains that cannot be claimed
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "app", "api", "admin", "mail", "smtp", "ftp", "ns1", "ns2",
        "dev", "staging", "test", "demo", "status", "blog", "docs", "help",
        "support", "cdn", "static", "assets", "media", "dashboard"
    };

    public SubdomainService(
        AppDbContext db,
        ITenantProvider tenantProvider,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<SubdomainService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SubdomainSuggestionsResponse> GetSuggestionsAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        var baseName = Slugify(tenant.Name);
        var candidates = new List<string>
        {
            baseName,
            $"{baseName}-restaurant",
            $"eat-at-{baseName}",
            $"{baseName}-dine",
            $"{baseName}-kitchen",
            $"{baseName}-cafe",
            $"the-{baseName}",
            $"{baseName}-eats"
        };

        // Remove duplicates and filter invalid
        candidates = candidates
            .Where(c => !string.IsNullOrEmpty(c) && c.Length >= 3 && c.Length <= 63)
            .Distinct()
            .Take(8)
            .ToList();

        // Check availability for all candidates (fetch existing subdomains, filter in memory)
        var allSubdomains = await _db.Tenants
            .Where(t => t.Subdomain != null)
            .Select(t => t.Subdomain!)
            .ToListAsync();

        var takenSubdomains = allSubdomains
            .Where(s => candidates.Contains(s, StringComparer.OrdinalIgnoreCase))
            .Select(s => s.ToLower())
            .ToList();

        var suggestions = candidates.Select(c => new SubdomainSuggestion
        {
            Subdomain = c,
            FullDomain = $"{c}.{BaseDomain}",
            IsAvailable = !takenSubdomains.Contains(c.ToLower()) && !ReservedSubdomains.Contains(c)
        }).ToList();

        return new SubdomainSuggestionsResponse { Suggestions = suggestions };
    }

    public async Task<CheckSubdomainResponse> CheckAvailabilityAsync(string subdomain)
    {
        subdomain = subdomain.ToLower().Trim();

        if (!IsValidSubdomain(subdomain))
        {
            return new CheckSubdomainResponse
            {
                Subdomain = subdomain,
                FullDomain = $"{subdomain}.{BaseDomain}",
                IsAvailable = false,
                Message = "Invalid subdomain. Use only lowercase letters, numbers, and hyphens (3-63 chars)."
            };
        }

        if (ReservedSubdomains.Contains(subdomain))
        {
            return new CheckSubdomainResponse
            {
                Subdomain = subdomain,
                FullDomain = $"{subdomain}.{BaseDomain}",
                IsAvailable = false,
                Message = "This subdomain is reserved."
            };
        }

        var tenantId = _tenantProvider.TenantId;

        var existingTenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == subdomain);

        // Available if no one has it, or current tenant already owns it
        var isAvailable = existingTenant == null ||
                          (tenantId != null && existingTenant.TenantId == tenantId);

        return new CheckSubdomainResponse
        {
            Subdomain = subdomain,
            FullDomain = $"{subdomain}.{BaseDomain}",
            IsAvailable = isAvailable,
            Message = isAvailable ? "This subdomain is available!" : "This subdomain is already taken."
        };
    }

    public async Task<SubdomainResponse> ClaimSubdomainAsync(string subdomain)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        subdomain = subdomain.ToLower().Trim();

        // Validate
        if (!IsValidSubdomain(subdomain))
            throw new ArgumentException("Invalid subdomain format.");

        if (ReservedSubdomains.Contains(subdomain))
            throw new ArgumentException("This subdomain is reserved.");

        // Check availability
        var existingTenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == subdomain);

        if (existingTenant != null && existingTenant.TenantId != tenantId)
            throw new InvalidOperationException("This subdomain is already taken.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        var oldSubdomain = tenant.Subdomain;

        // Create DNS record via Hostinger API
        var dnsStatus = "pending";
        try
        {
            await CreateHostingerDnsRecord(subdomain);
            dnsStatus = "active";

            // If tenant had a different subdomain, remove old DNS record
            if (!string.IsNullOrEmpty(oldSubdomain) && oldSubdomain.ToLower() != subdomain)
            {
                try { await DeleteHostingerDnsRecord(oldSubdomain); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete old DNS record for {OldSubdomain}", oldSubdomain); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DNS record for {Subdomain}", subdomain);
            dnsStatus = "dns_failed";
        }

        // Save subdomain to tenant
        tenant.Subdomain = subdomain;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new SubdomainResponse
        {
            Subdomain = subdomain,
            FullDomain = $"{subdomain}.{BaseDomain}",
            IsActive = true,
            DnsStatus = dnsStatus,
            Message = dnsStatus == "active"
                ? $"Subdomain {subdomain}.{BaseDomain} is now live!"
                : $"Subdomain saved but DNS creation failed. It may take a few minutes to propagate."
        };
    }

    public async Task<SubdomainResponse> ReleaseSubdomainAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        if (string.IsNullOrEmpty(tenant.Subdomain))
        {
            return new SubdomainResponse
            {
                IsActive = false,
                Message = "No subdomain is currently assigned."
            };
        }

        var oldSubdomain = tenant.Subdomain;

        // Remove DNS record
        try
        {
            await DeleteHostingerDnsRecord(oldSubdomain);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete DNS record for {Subdomain}", oldSubdomain);
        }

        tenant.Subdomain = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new SubdomainResponse
        {
            IsActive = false,
            DnsStatus = "removed",
            Message = $"Subdomain {oldSubdomain}.{BaseDomain} has been released."
        };
    }

    public async Task<SubdomainResponse> GetCurrentSubdomainAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        if (string.IsNullOrEmpty(tenant.Subdomain))
        {
            return new SubdomainResponse
            {
                IsActive = false,
                Message = "No subdomain assigned yet."
            };
        }

        return new SubdomainResponse
        {
            Subdomain = tenant.Subdomain,
            FullDomain = $"{tenant.Subdomain}.{BaseDomain}",
            IsActive = true,
            DnsStatus = "active"
        };
    }

    public async Task<string?> ResolveTenantIdAsync(string subdomain)
    {
        subdomain = subdomain.ToLower().Trim();

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == subdomain && t.IsActive);

        return tenant?.TenantId;
    }

    // ═══════════════════════════════════════════════════
    // Hostinger DNS API Integration
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Create a CNAME record on Hostinger pointing subdomain.tabverse.in → your Azure app.
    /// Hostinger DNS API: https://developers.hostinger.com
    /// </summary>
    private async Task CreateHostingerDnsRecord(string subdomain)
    {
        var apiToken = _config["Hostinger:ApiToken"];
        if (string.IsNullOrEmpty(apiToken))
        {
            _logger.LogWarning("Hostinger API token not configured. Skipping DNS record creation.");
            return;
        }

        var targetValue = _config["Hostinger:CnameTarget"] ?? "tabverse.in";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        // Hostinger DNS API - Add DNS zone record
        // POST https://developers.hostinger.com/api/dns/v1/zones/{domain}/records
        var url = $"https://developers.hostinger.com/api/dns/v1/zones/{BaseDomain}/records";

        var payload = new[]
        {
            new
            {
                type = "CNAME",
                name = subdomain,
                content = targetValue,
                ttl = 3600
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Hostinger DNS API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new Exception($"Hostinger DNS API returned {response.StatusCode}: {responseBody}");
        }

        _logger.LogInformation("DNS CNAME record created: {Subdomain}.{Domain} → {Target}",
            subdomain, BaseDomain, targetValue);
    }

    /// <summary>
    /// Delete a DNS record from Hostinger.
    /// </summary>
    private async Task DeleteHostingerDnsRecord(string subdomain)
    {
        var apiToken = _config["Hostinger:ApiToken"];
        if (string.IsNullOrEmpty(apiToken))
        {
            _logger.LogWarning("Hostinger API token not configured. Skipping DNS record deletion.");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        // First, list DNS records to find the record ID
        var listUrl = $"https://developers.hostinger.com/api/dns/v1/zones/{BaseDomain}/records";
        var listResponse = await client.GetAsync(listUrl);
        var listBody = await listResponse.Content.ReadAsStringAsync();

        if (!listResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to list DNS records: {StatusCode}", listResponse.StatusCode);
            return;
        }

        // Parse response to find matching record IDs
        using var doc = JsonDocument.Parse(listBody);
        var recordIds = new List<long>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var record in doc.RootElement.EnumerateArray())
            {
                var name = record.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (name != null && name.Equals(subdomain, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.TryGetProperty("id", out var idProp))
                    {
                        recordIds.Add(idProp.GetInt64());
                    }
                }
            }
        }

        if (recordIds.Count == 0)
        {
            _logger.LogInformation("No DNS record found for {Subdomain} to delete.", subdomain);
            return;
        }

        // Delete the records
        // DELETE https://developers.hostinger.com/api/dns/v1/zones/{domain}/records
        var deleteUrl = $"https://developers.hostinger.com/api/dns/v1/zones/{BaseDomain}/records";
        var deletePayload = new { ids = recordIds };
        var deleteJson = JsonSerializer.Serialize(deletePayload);

        var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl)
        {
            Content = new StringContent(deleteJson, Encoding.UTF8, "application/json")
        };
        var deleteResponse = await client.SendAsync(request);

        if (!deleteResponse.IsSuccessStatusCode)
        {
            var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to delete DNS record: {StatusCode} - {Body}", deleteResponse.StatusCode, deleteBody);
        }
        else
        {
            _logger.LogInformation("DNS record deleted for {Subdomain}.{Domain}", subdomain, BaseDomain);
        }
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var slug = name.ToLower().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");  // Remove special chars
        slug = Regex.Replace(slug, @"\s+", "-");            // Spaces to hyphens
        slug = Regex.Replace(slug, @"-+", "-");             // Multiple hyphens to single
        slug = slug.Trim('-');

        if (slug.Length > 63) slug = slug[..63].TrimEnd('-');
        return slug;
    }

    private static bool IsValidSubdomain(string subdomain)
    {
        if (string.IsNullOrEmpty(subdomain) || subdomain.Length < 3 || subdomain.Length > 63)
            return false;

        return Regex.IsMatch(subdomain, @"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$");
    }
}
