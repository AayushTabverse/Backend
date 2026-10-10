using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using menu_backend.Data;
using menu_backend.DTOs.Website;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace menu_backend.Services;

/// <summary>
/// Restaurant subdomains (name.tabverse.in): availability, claiming, DNS records via the Hostinger
/// API, and an honest website status built from facts — the stored DNS outcome, whether the address
/// resolves, and whether the site actually responds.
/// </summary>
public class SubdomainService : ISubdomainService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IWebsiteService _websiteService;
    private readonly SiteProbe _probe;
    private readonly ILogger<SubdomainService> _logger;

    private const string HostingerApi = "https://developers.hostinger.com/api/dns/v1/zones";

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
        IMemoryCache cache,
        IWebsiteService websiteService,
        SiteProbe probe,
        ILogger<SubdomainService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _websiteService = websiteService;
        _probe = probe;
        _logger = logger;
    }

    /// <summary>Zone that restaurant subdomains live under (config App:SiteDomain, default tabverse.in).</summary>
    private string BaseDomain => _config["App:SiteDomain"] ?? "tabverse.in";

    /// <summary>Local development: *.localhost resolves without any DNS record.</summary>
    private bool IsLocalDomain => BaseDomain.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private string SiteUrl(string subdomain) =>
        string.Format(_config["App:SiteUrlTemplate"] ?? $"https://{{0}}.{BaseDomain}", subdomain);

    private string FullDomain(string subdomain) => $"{subdomain}.{BaseDomain}";

    // ═══════════════════════════════════════════════════
    // Suggestions & availability
    // ═══════════════════════════════════════════════════

    public async Task<SubdomainSuggestionsResponse> GetSuggestionsAsync()
    {
        var tenant = await CurrentTenantAsync();

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

        candidates = candidates
            .Where(c => !string.IsNullOrEmpty(c) && c.Length >= 3 && c.Length <= 63)
            .Distinct()
            .Take(8)
            .ToList();

        // Fetch existing subdomains and filter in memory
        var allSubdomains = await _db.Tenants
            .Where(t => t.Subdomain != null && t.TenantId != tenant.TenantId)
            .Select(t => t.Subdomain!)
            .ToListAsync();

        var taken = allSubdomains
            .Where(s => candidates.Contains(s, StringComparer.OrdinalIgnoreCase))
            .Select(s => s.ToLower())
            .ToHashSet();

        return new SubdomainSuggestionsResponse
        {
            Suggestions = candidates.Select(c => new SubdomainSuggestion
            {
                Subdomain = c,
                FullDomain = FullDomain(c),
                IsAvailable = !taken.Contains(c.ToLower()) && !ReservedSubdomains.Contains(c)
            }).ToList()
        };
    }

    public async Task<CheckSubdomainResponse> CheckAvailabilityAsync(string subdomain)
    {
        subdomain = subdomain.ToLower().Trim();
        var problem = await AvailabilityProblemAsync(subdomain, _tenantProvider.TenantId);

        return new CheckSubdomainResponse
        {
            Subdomain = subdomain,
            FullDomain = FullDomain(subdomain),
            IsAvailable = problem == null,
            Message = problem ?? "This subdomain is available!"
        };
    }

    /// <summary>Null when the subdomain can be used by this tenant, otherwise the reason it can't.</summary>
    private async Task<string?> AvailabilityProblemAsync(string subdomain, string? tenantId)
    {
        if (!IsValidSubdomain(subdomain))
            return "Invalid subdomain. Use only lowercase letters, numbers, and hyphens (3-63 chars).";
        if (ReservedSubdomains.Contains(subdomain))
            return "This subdomain is reserved.";

        var owner = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == subdomain);
        return owner == null || owner.TenantId == tenantId ? null : "This subdomain is already taken.";
    }

    // ═══════════════════════════════════════════════════
    // Claim / publish / retry / release
    // ═══════════════════════════════════════════════════

    public async Task<SubdomainResponse> ClaimSubdomainAsync(string subdomain)
    {
        var tenant = await CurrentTenantAsync();
        await AssignSubdomainAsync(tenant, subdomain.ToLower().Trim());
        return ToSubdomainResponse(tenant);
    }

    public async Task<WebsiteStatusResponse> PublishAsync(string subdomain)
    {
        var tenant = await CurrentTenantAsync();
        subdomain = subdomain.ToLower().Trim();

        // Re-run DNS when the address changes or the last attempt didn't succeed
        if (!string.Equals(tenant.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)
            || tenant.SubdomainDnsStatus != "created")
            await AssignSubdomainAsync(tenant, subdomain);

        await _websiteService.GetWebsiteContentAsync(); // creates default content if it doesn't exist yet
        var content = await CurrentContentAsync(tenant.TenantId);
        if (content != null && !content.IsPublished)
        {
            content.IsPublished = true;
            content.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return await GetStatusAsync(forceCheck: true);
    }

    public async Task<WebsiteStatusResponse> RetryDnsAsync()
    {
        var tenant = await CurrentTenantAsync();
        if (string.IsNullOrEmpty(tenant.Subdomain))
            throw new InvalidOperationException("Choose a web address first.");

        await ApplyDnsResultAsync(tenant, await CreateDnsRecordAsync(tenant.Subdomain));
        return await GetStatusAsync(forceCheck: true);
    }

    public async Task<SubdomainResponse> ReleaseSubdomainAsync()
    {
        var tenant = await CurrentTenantAsync();

        if (string.IsNullOrEmpty(tenant.Subdomain))
            return new SubdomainResponse { IsActive = false, Message = "No subdomain is currently assigned." };

        var oldSubdomain = tenant.Subdomain;
        await DeleteDnsRecordAsync(oldSubdomain);

        tenant.Subdomain = null;
        tenant.SubdomainDnsStatus = null;
        tenant.SubdomainDnsError = null;
        tenant.SubdomainDnsUpdatedAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove(StatusCacheKey(tenant.TenantId));

        return new SubdomainResponse
        {
            IsActive = false,
            DnsStatus = "removed",
            Message = $"{FullDomain(oldSubdomain)} has been removed."
        };
    }

    public async Task<SubdomainResponse> GetCurrentSubdomainAsync() => ToSubdomainResponse(await CurrentTenantAsync());

    public async Task<string?> ResolveTenantIdAsync(string subdomain)
    {
        subdomain = subdomain.ToLower().Trim();
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain != null && t.Subdomain.ToLower() == subdomain && t.IsActive);
        return tenant?.TenantId;
    }

    private async Task AssignSubdomainAsync(Tenant tenant, string subdomain)
    {
        var problem = await AvailabilityProblemAsync(subdomain, tenant.TenantId);
        if (problem != null)
        {
            if (problem.Contains("taken")) throw new InvalidOperationException(problem);
            throw new ArgumentException(problem);
        }

        var oldSubdomain = tenant.Subdomain;
        var changing = !string.IsNullOrEmpty(oldSubdomain) && !oldSubdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase);

        var result = await CreateDnsRecordAsync(subdomain);
        if (changing && result.Status == "created")
            await DeleteDnsRecordAsync(oldSubdomain!);

        tenant.Subdomain = subdomain;
        await ApplyDnsResultAsync(tenant, result);
    }

    private async Task ApplyDnsResultAsync(Tenant tenant, DnsResult result)
    {
        tenant.SubdomainDnsStatus = result.Status;
        tenant.SubdomainDnsError = result.Error;
        tenant.SubdomainDnsUpdatedAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove(StatusCacheKey(tenant.TenantId));
    }

    private SubdomainResponse ToSubdomainResponse(Tenant tenant)
    {
        if (string.IsNullOrEmpty(tenant.Subdomain))
            return new SubdomainResponse { IsActive = false, Message = "No subdomain assigned yet." };

        var created = tenant.SubdomainDnsStatus == "created";
        return new SubdomainResponse
        {
            Subdomain = tenant.Subdomain,
            FullDomain = FullDomain(tenant.Subdomain),
            IsActive = created,
            DnsStatus = tenant.SubdomainDnsStatus ?? "pending",
            Message = created
                ? $"{FullDomain(tenant.Subdomain)} is reserved for you. It can take a few minutes to start working."
                : tenant.SubdomainDnsError ?? "The web address isn't set up yet."
        };
    }

    // ═══════════════════════════════════════════════════
    // Website status
    // ═══════════════════════════════════════════════════

    private static string StatusCacheKey(string tenantId) => $"website-status:{tenantId}";

    public async Task<WebsiteStatusResponse> GetStatusAsync(bool forceCheck = false)
    {
        var tenant = await CurrentTenantAsync();
        var cacheKey = StatusCacheKey(tenant.TenantId);
        if (!forceCheck && _cache.TryGetValue(cacheKey, out WebsiteStatusResponse? cached) && cached != null)
            return cached;

        var content = await CurrentContentAsync(tenant.TenantId);
        var isPublished = content?.IsPublished ?? false;
        var status = new WebsiteStatusResponse
        {
            Subdomain = tenant.Subdomain,
            IsPublished = isPublished,
            PreviewPath = $"/website/{tenant.TenantId}",
            CheckedAt = DateTime.UtcNow
        };

        var publishStep = new WebsiteStatusStep
        {
            Key = "published",
            Label = "Website published",
            Status = isPublished ? "done" : "todo",
            Detail = isPublished ? "Visitors can see your website." : "Hidden from visitors until you publish."
        };

        // ── No web address yet ──
        if (string.IsNullOrEmpty(tenant.Subdomain))
        {
            status.Steps.Add(new WebsiteStatusStep { Key = "address", Label = "Web address chosen", Status = "todo", Detail = $"Pick a name.{BaseDomain} address below." });
            status.Steps.Add(publishStep);
            if (isPublished)
            {
                var frontend = (_config["App:FrontendUrl"] ?? "https://tabverse.in").TrimEnd('/');
                status.State = "published_no_domain";
                status.Url = frontend + status.PreviewPath;
                status.Title = "Published — choose your web address";
                status.Message = $"Your website is visible at {status.Url}. Pick a name.{BaseDomain} address to get a short, shareable link.";
            }
            else
            {
                status.State = "not_started";
                status.Title = "Your website isn't live yet";
                status.Message = $"Choose a name.{BaseDomain} address and press Publish website.";
            }
            return Cache(cacheKey, status);
        }

        var domain = FullDomain(tenant.Subdomain);
        status.Url = SiteUrl(tenant.Subdomain);
        status.Steps.Add(new WebsiteStatusStep { Key = "address", Label = "Web address reserved", Status = "done", Detail = domain });

        // ── DNS record ──
        var dnsStep = new WebsiteStatusStep { Key = "dns", Label = "Address set up (DNS)" };
        status.Steps.Add(dnsStep);

        // Subdomains claimed before DNS outcomes were recorded: trust the live check if it works
        var dnsStatus = tenant.SubdomainDnsStatus;
        SiteProbe.Result? reach = null;
        if (dnsStatus == null)
        {
            reach = await _probe.CheckAsync(SiteUrl(tenant.Subdomain));
            if (reach.Resolves) dnsStatus = "created";
        }

        switch (dnsStatus)
        {
            case "created":
                dnsStep.Status = "done";
                dnsStep.Detail = $"{domain} points to Tabverse.";
                break;
            case "not_configured":
                dnsStep.Status = "failed";
                dnsStep.Detail = "Automatic address setup isn't configured on the server.";
                break;
            case "failed":
                dnsStep.Status = "failed";
                dnsStep.Detail = tenant.SubdomainDnsError ?? "The DNS provider returned an error.";
                break;
            default:
                dnsStep.Status = "failed";
                dnsStep.Detail = "Address setup hasn't run yet.";
                break;
        }

        // ── Live checks (only meaningful once the record exists) ──
        var reachStep = new WebsiteStatusStep { Key = "reachable", Label = "Address working" };
        status.Steps.Add(reachStep);
        status.Steps.Add(publishStep);

        if (dnsStep.Status != "done")
        {
            reachStep.Status = "todo";
            reachStep.Detail = "Waiting for the address to be set up.";
            status.CanRetryDns = true;
            status.State = dnsStatus == "not_configured" ? "dns_not_configured" : "dns_failed";
            status.Title = $"We couldn't set up {domain}";
            status.Message = dnsStatus == "not_configured"
                ? "Automatic web address setup isn't available right now. Please contact Tabverse support — your address is reserved and nobody else can take it."
                : $"{dnsStep.Detail} Your address is reserved; press Retry, and contact Tabverse support if it keeps failing.";
            return Cache(cacheKey, status);
        }

        reach ??= await _probe.CheckAsync(SiteUrl(tenant.Subdomain));
        reachStep.Status = reach.Ok ? "done" : "pending";
        reachStep.Detail = reach.Ok ? $"{domain} is responding." : reach.Reason;

        if (!isPublished)
        {
            status.State = "unpublished";
            status.Title = "Your website is unpublished";
            status.Message = $"{domain} is reserved, but visitors see \"not published yet\". Press Publish website to make it visible.";
        }
        else if (reach.Ok)
        {
            status.State = "live";
            status.Title = "Your website is live 🎉";
            status.Message = $"Anyone can visit {status.Url}. Share it on WhatsApp, Instagram and Google Maps.";
        }
        else
        {
            status.ShouldRecheck = true;
            status.State = reach.Resolves ? "unreachable" : "connecting";
            status.Title = reach.Resolves ? $"{domain} is almost ready" : $"Setting up {domain}…";
            status.Message = reach.Resolves
                ? reach.Reason.Contains("certificate")
                    ? "The address is set up and the secure (https) certificate is being issued. This usually takes under an hour — we'll keep checking."
                    : $"The address is set up, but the site isn't responding yet ({reach.Reason}). This usually fixes itself within an hour; contact Tabverse support if it doesn't. We'll keep checking."
                : "Your address has been created. New addresses usually start working within 5–30 minutes (occasionally a few hours). We'll keep checking — you can leave this page.";
        }

        return Cache(cacheKey, status);
    }

    private WebsiteStatusResponse Cache(string key, WebsiteStatusResponse status)
    {
        // Short cache: the editor polls while a site is coming up
        _cache.Set(key, status, TimeSpan.FromSeconds(15));
        return status;
    }

    // ═══════════════════════════════════════════════════
    // Hostinger DNS API
    // PUT    /api/dns/v1/zones/{domain}  { overwrite, zone: [{ name, type, ttl, records: [{ content }] }] }
    // DELETE /api/dns/v1/zones/{domain}  { filters: [{ name, type }] }
    // ═══════════════════════════════════════════════════

    private record DnsResult(string Status, string? Error);

    /// <summary>Creates (or replaces) the CNAME name.{BaseDomain} → CnameTarget. Never throws.</summary>
    private async Task<DnsResult> CreateDnsRecordAsync(string subdomain)
    {
        if (IsLocalDomain) return new("created", null);

        var apiToken = _config["Hostinger:ApiToken"];
        if (string.IsNullOrEmpty(apiToken))
        {
            _logger.LogWarning("Hostinger API token not configured; cannot create DNS record for {Subdomain}.", subdomain);
            return new("not_configured", "Automatic address setup isn't configured on the server.");
        }

        // Fully-qualified target (trailing dot) so the provider doesn't append the zone name
        var target = (_config["Hostinger:CnameTarget"] ?? BaseDomain).TrimEnd('.') + ".";
        var payload = new
        {
            overwrite = true, // replaces an existing CNAME for this name, so retries are safe
            zone = new[]
            {
                new { name = subdomain, type = "CNAME", ttl = 3600, records = new[] { new { content = target } } }
            }
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var request = new HttpRequestMessage(HttpMethod.Put, $"{HostingerApi}/{BaseDomain}")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("DNS CNAME created: {Subdomain}.{Domain} → {Target}", subdomain, BaseDomain, target);
                return new("created", null);
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Hostinger DNS API error {Status} for {Subdomain}: {Body}", (int)response.StatusCode, subdomain, body);
            return new("failed", (int)response.StatusCode switch
            {
                401 or 403 => "The DNS provider rejected Tabverse's credentials.",
                404 => $"The DNS zone for {BaseDomain} wasn't found at the provider.",
                422 => "The DNS provider rejected the address record.",
                429 => "The DNS provider is rate-limiting requests. Try again in a minute.",
                _ => $"The DNS provider returned an error (HTTP {(int)response.StatusCode})."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hostinger DNS API call failed for {Subdomain}", subdomain);
            return new("failed", "Couldn't reach the DNS provider. Try again in a minute.");
        }
    }

    /// <summary>Removes the CNAME for the subdomain. Failures are logged, not thrown.</summary>
    private async Task DeleteDnsRecordAsync(string subdomain)
    {
        if (IsLocalDomain) return;
        var apiToken = _config["Hostinger:ApiToken"];
        if (string.IsNullOrEmpty(apiToken)) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            var body = JsonSerializer.Serialize(new { filters = new[] { new { name = subdomain, type = "CNAME" } } });
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{HostingerApi}/{BaseDomain}")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Failed to delete DNS record for {Subdomain}: {Status} {Body}",
                    subdomain, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete DNS record for {Subdomain}", subdomain);
        }
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private async Task<Tenant> CurrentTenantAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");
        return await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");
    }

    private Task<WebsiteContent?> CurrentContentAsync(string tenantId) =>
        _db.WebsiteContents.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted);

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
