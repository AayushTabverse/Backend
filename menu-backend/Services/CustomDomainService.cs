using System.Globalization;
using System.Net;
using System.Security.Cryptography;
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
/// Lets a restaurant connect a domain it already owns (www.spicegarden.in or spicegarden.in).
///
/// Flow: owner enters the domain → we show the DNS records to add (an ownership TXT record plus a
/// CNAME / A record pointing at Tabverse, and Azure's asuid TXT when configured) → we check them over
/// DNS-over-HTTPS → once DNS is right, the domain still has to be added to the Azure App Service with
/// an HTTPS certificate → we probe the domain and report Live only when it actually serves the site.
/// </summary>
public class CustomDomainService : ICustomDomainService
{
    /// <summary>Second-level suffixes where the registrable root has three labels (spicegarden.co.in).</summary>
    private static readonly HashSet<string> MultiLabelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.in", "net.in", "org.in", "firm.in", "gen.in", "ind.in", "ac.in", "edu.in", "res.in", "gov.in",
        "co.uk", "org.uk", "com.au", "net.au", "co.nz", "com.sg", "com.my", "co.za", "com.br", "co.jp", "com.np", "com.bd", "com.pk"
    };

    private static readonly Regex Label = new(@"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$");

    /// <summary>Unverified claims older than this no longer block another restaurant from the domain.</summary>
    private static readonly TimeSpan StaleClaim = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly DnsOverHttps _dns;
    private readonly SiteProbe _probe;
    private readonly IMemoryCache _cache;

    public CustomDomainService(AppDbContext db, ITenantProvider tenantProvider, IConfiguration config,
        IWebHostEnvironment env, DnsOverHttps dns, SiteProbe probe, IMemoryCache cache)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _env = env;
        _dns = dns;
        _probe = probe;
        _cache = cache;
    }

    private string SiteDomain => _config["App:SiteDomain"] ?? "tabverse.in";

    /// <summary>Hostname customer CNAMEs should point at (config CustomDomains:CnameTarget).</summary>
    private string CnameTarget => (_config["CustomDomains:CnameTarget"] ?? SiteDomain).TrimEnd('.').ToLowerInvariant();

    /// <summary>
    /// Development only: two-label *.localhost names (www.spice.localhost) act as "custom domains" that
    /// need no DNS, so the whole flow can be tested locally. Single-label name.localhost is a Tabverse subdomain.
    /// </summary>
    private bool IsLocalTestDomain(string domain) =>
        _env.IsDevelopment() && domain.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) && domain.Split('.').Length >= 3;

    private string UrlFor(string domain) =>
        IsLocalTestDomain(domain) ? $"http://{domain}:4201" : $"https://{domain}";

    private static string CacheKey(string tenantId) => $"custom-domain-status:{tenantId}";

    // ═══════════════════════════════════════════════════
    // Connect / remove
    // ═══════════════════════════════════════════════════

    public async Task<CustomDomainStatusResponse> ConnectAsync(string input)
    {
        var tenant = await CurrentTenantAsync();
        var domain = Normalize(input);

        if (!string.Equals(tenant.CustomDomain, domain, StringComparison.OrdinalIgnoreCase))
        {
            var other = await _db.Tenants.FirstOrDefaultAsync(t => t.CustomDomain == domain && t.TenantId != tenant.TenantId);
            if (other != null)
            {
                var stale = other.CustomDomainVerifiedAt == null && other.CustomDomainAddedAt < DateTime.UtcNow - StaleClaim;
                if (!stale)
                    throw new InvalidOperationException(other.CustomDomainVerifiedAt != null
                        ? $"{domain} is already connected to another restaurant on Tabverse. If you own it, contact Tabverse support."
                        : $"{domain} is being connected by another Tabverse account. If you own it, contact Tabverse support.");
                ClearCustomDomain(other);
            }

            tenant.CustomDomain = domain;
            tenant.CustomDomainToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            tenant.CustomDomainAddedAt = DateTime.UtcNow;
            tenant.CustomDomainVerifiedAt = null;
            tenant.CustomDomainActiveAt = null;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        _cache.Remove(CacheKey(tenant.TenantId));
        return await GetStatusAsync(forceCheck: true);
    }

    public async Task RemoveAsync()
    {
        var tenant = await CurrentTenantAsync();
        ClearCustomDomain(tenant);
        await _db.SaveChangesAsync();
        _cache.Remove(CacheKey(tenant.TenantId));
    }

    private static void ClearCustomDomain(Tenant t)
    {
        t.CustomDomain = null;
        t.CustomDomainToken = null;
        t.CustomDomainAddedAt = null;
        t.CustomDomainVerifiedAt = null;
        t.CustomDomainActiveAt = null;
        t.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<string?> ResolveTenantIdAsync(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t =>
            t.CustomDomain == host && t.CustomDomainVerifiedAt != null && t.IsActive);
        return tenant?.TenantId;
    }

    // ═══════════════════════════════════════════════════
    // Status
    // ═══════════════════════════════════════════════════

    public async Task<CustomDomainStatusResponse> GetStatusAsync(bool forceCheck = false)
    {
        var tenant = await CurrentTenantAsync();
        if (!forceCheck && _cache.TryGetValue(CacheKey(tenant.TenantId), out CustomDomainStatusResponse? cached) && cached != null)
            return cached;

        var status = new CustomDomainStatusResponse { CheckedAt = DateTime.UtcNow };
        if (string.IsNullOrEmpty(tenant.CustomDomain))
        {
            status.State = "none";
            status.Title = "Use a domain you already own";
            status.Message = "Connect a domain like www.yourrestaurant.in. You'll add a couple of DNS records at your domain provider; we'll check them for you.";
            return status;
        }

        var domain = tenant.CustomDomain;
        var root = RootDomain(domain);
        status.Domain = domain;
        status.RootDomain = root;
        status.IsApex = domain == root;
        status.Url = UrlFor(domain);

        // ── DNS records and their live checks ──
        var local = IsLocalTestDomain(domain);
        var ownership = await CheckOwnershipAsync(tenant, root, local);
        var pointing = await CheckPointingAsync(domain, root, status.IsApex, local);
        status.Records.Add(ownership);
        status.Records.Add(pointing);
        var hostingId = _config["CustomDomains:AzureVerificationId"];
        DnsRecordInstruction? hosting = null;
        if (!string.IsNullOrWhiteSpace(hostingId))
        {
            hosting = await CheckTxtAsync("hosting", RecordName("asuid", domain), root, hostingId.Trim(), local,
                "Lets Tabverse's hosting provider (Azure) accept your domain.");
            status.Records.Add(hosting);
        }

        if (ownership.Status == "ok" && tenant.CustomDomainVerifiedAt == null)
            tenant.CustomDomainVerifiedAt = DateTime.UtcNow;
        var verified = tenant.CustomDomainVerifiedAt != null;
        var dnsReady = verified && pointing.Status == "ok" && (hosting == null || hosting.Status == "ok");

        // ── Does the domain actually serve the site? ──
        SiteProbe.Result? reach = null;
        if (dnsReady)
        {
            reach = await _probe.CheckAsync(status.Url);
            if (reach.Ok && tenant.CustomDomainActiveAt == null) tenant.CustomDomainActiveAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        var isPublished = await _db.WebsiteContents.IgnoreQueryFilters()
            .AnyAsync(w => w.TenantId == tenant.TenantId && !w.IsDeleted && w.IsPublished);

        status.Steps.Add(new WebsiteStatusStep { Key = "address", Label = "Domain added", Status = "done", Detail = domain });
        status.Steps.Add(new WebsiteStatusStep
        {
            Key = "ownership", Label = "Ownership verified",
            Status = verified ? "done" : "pending",
            Detail = verified ? "Your TXT record proves you control this domain." : "Waiting for the TXT record below."
        });
        status.Steps.Add(new WebsiteStatusStep
        {
            Key = "dns", Label = "Pointed to Tabverse",
            Status = pointing.Status == "ok" && (hosting == null || hosting.Status == "ok") ? "done" : pointing.Status == "wrong" ? "failed" : "pending",
            Detail = pointing.Status == "ok" ? $"{domain} points to Tabverse." : pointing.Status == "wrong"
                ? $"{domain} points somewhere else ({pointing.Found}). Update the {pointing.Type} record below."
                : $"Waiting for the {pointing.Type} record below."
        });
        status.Steps.Add(new WebsiteStatusStep
        {
            Key = "reachable", Label = "Connected & secure (https)",
            Status = reach?.Ok == true ? "done" : dnsReady ? "pending" : "todo",
            Detail = reach?.Ok == true ? $"{domain} is serving your website." : dnsReady ? Cap(reach!.Reason) + "." : "After your DNS records are in place."
        });
        status.Steps.Add(new WebsiteStatusStep
        {
            Key = "published", Label = "Website published",
            Status = isPublished ? "done" : "todo",
            Detail = isPublished ? "Visitors can see your website." : "Hidden from visitors until you publish."
        });

        if (!dnsReady)
        {
            status.State = "dns_pending";
            status.ShouldRecheck = true;
            var wrong = status.Records.Where(r => r.Status == "wrong").ToList();
            status.Title = wrong.Count > 0 ? $"Fix the DNS records for {domain}" : $"Add these DNS records for {domain}";
            status.Message = wrong.Count > 0
                ? $"Some records point to the wrong place: update them at the company where you bought {root} (GoDaddy, Hostinger, BigRock…)."
                : $"Log in where you bought {root} (GoDaddy, Hostinger, BigRock…), open DNS settings and add the records below. Changes usually show up within 5–30 minutes; we'll keep checking.";
        }
        else if (!reach!.Ok)
        {
            status.State = "activating";
            status.ShouldRecheck = true;
            status.Title = $"{domain} is verified — connecting it to Tabverse";
            status.Message = "Your DNS records are correct. We're connecting your domain to our servers and issuing its secure (https) certificate. This can take up to 24 hours — we'll keep checking and this will turn green by itself.";
        }
        else if (!isPublished)
        {
            status.State = "unpublished";
            status.Title = $"{domain} is connected, but your website is unpublished";
            status.Message = "Visitors see \"Coming soon\". Publish your website to make it visible.";
        }
        else
        {
            status.State = "live";
            status.Title = $"{domain} is live 🎉";
            status.Message = $"Your website is now at {status.Url}. You can share this address everywhere.";
        }

        _cache.Set(CacheKey(tenant.TenantId), status, TimeSpan.FromSeconds(15));
        return status;
    }

    private async Task<DnsRecordInstruction> CheckOwnershipAsync(Tenant tenant, string root, bool local)
    {
        var record = await CheckTxtAsync("ownership", RecordName("_tabverse-verify", tenant.CustomDomain!), root,
            $"tabverse-verify={tenant.CustomDomainToken}", local,
            "Proves you own this domain. You can delete it after your website is live.");
        // Once verified, removing the TXT record later doesn't un-verify the domain
        if (record.Status != "ok" && tenant.CustomDomainVerifiedAt != null) record.Status = "ok";
        return record;
    }

    private async Task<DnsRecordInstruction> CheckTxtAsync(string purpose, string fullName, string root, string expected, bool local, string description)
    {
        var record = new DnsRecordInstruction
        {
            Purpose = purpose, Type = "TXT", FullName = fullName, Host = RelativeHost(fullName, root),
            Value = expected, Description = description
        };
        if (local) { record.Status = "ok"; return record; }

        var values = await _dns.LookupAsync(fullName, "TXT");
        record.Status = values == null ? "missing" : values.Contains(expected) ? "ok" : values.Count > 0 ? "wrong" : "missing";
        if (record.Status == "wrong") record.Found = string.Join(", ", values!.Take(3));
        return record;
    }

    private async Task<DnsRecordInstruction> CheckPointingAsync(string domain, string root, bool isApex, bool local)
    {
        var targetIps = await TargetIpsAsync();
        var record = isApex
            ? new DnsRecordInstruction
            {
                Purpose = "pointing", Type = "A", FullName = domain, Host = "@",
                Value = targetIps.FirstOrDefault() ?? "(Tabverse IP unavailable — try again shortly)",
                Description = $"Points {domain} to Tabverse. Remove any other A records for @ (and AAAA records) so visitors always reach your website."
            }
            : new DnsRecordInstruction
            {
                Purpose = "pointing", Type = "CNAME", FullName = domain, Host = RelativeHost(domain, root), Value = CnameTarget,
                Description = $"Points {domain} to Tabverse. If a record with this name already exists (often CNAME → {root} or a parking page), edit it instead of adding a new one."
            };
        if (local) { record.Status = "ok"; return record; }

        if (!isApex)
        {
            var cnames = await _dns.LookupAsync(domain, "CNAME");
            if (cnames?.Contains(CnameTarget) == true) { record.Status = "ok"; return record; }
            if (cnames?.Count > 0) { record.Status = "wrong"; record.Found = $"CNAME → {cnames[0]}"; return record; }
        }

        // Root domains use A records; also accept A records (or CNAME flattening) on subdomains if they match
        var ips = await _dns.LookupAsync(domain, "A");
        if (ips == null || ips.Count == 0) { record.Status = "missing"; return record; }
        if (targetIps.Count > 0 && ips.All(targetIps.Contains)) { record.Status = "ok"; return record; }
        record.Status = "wrong";
        record.Found = $"A → {string.Join(", ", ips.Take(3))}";
        return record;
    }

    /// <summary>Tabverse's public IPs (config CustomDomains:ARecord, else whatever the CNAME target resolves to).</summary>
    private async Task<List<string>> TargetIpsAsync()
    {
        var configured = _config["CustomDomains:ARecord"];
        if (!string.IsNullOrWhiteSpace(configured)) return new List<string> { configured.Trim() };
        return await _cache.GetOrCreateAsync("custom-domain-target-ips", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _dns.LookupAsync(CnameTarget, "A") ?? new List<string>();
        }) ?? new List<string>();
    }

    // ═══════════════════════════════════════════════════
    // Domain helpers
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// "https://WWW.SpiceGarden.in/menu" → "www.spicegarden.in" (punycode for non-Latin names).
    /// Throws ArgumentException with an owner-facing message for anything we can't connect.
    /// </summary>
    private string Normalize(string input)
    {
        var d = (input ?? "").Trim().ToLowerInvariant();
        d = Regex.Replace(d, @"^[a-z]+://", "");
        d = d.Split('/', '?', '#')[0].Split(':')[0].TrimEnd('.');

        if (d.Length == 0) throw new ArgumentException("Enter your domain, e.g. www.yourrestaurant.in.");
        try { d = new IdnMapping().GetAscii(d); }
        catch (ArgumentException) { throw new ArgumentException($"\"{input}\" isn't a valid domain name."); }

        if (IPAddress.TryParse(d, out _)) throw new ArgumentException("Enter a domain name, not an IP address.");
        var isTabverseSubdomain = d.EndsWith("." + SiteDomain) && d.Split('.').Length == SiteDomain.Split('.').Length + 1;
        if (d == SiteDomain || isTabverseSubdomain || d == "tabverse.in" || d.EndsWith(".tabverse.in"))
            throw new ArgumentException($"That's a Tabverse address — use the \"Your web address\" option above for name.{SiteDomain}.");
        if (d.EndsWith(".localhost") && !_env.IsDevelopment())
            throw new ArgumentException($"\"{input}\" isn't a public domain.");

        var labels = d.Split('.');
        if (d.Length > 253 || labels.Length < 2 || labels.Any(l => !Label.IsMatch(l)) || !Regex.IsMatch(labels[^1], @"^([a-z]{2,63}|xn--[a-z0-9\-]+)$"))
            throw new ArgumentException($"\"{input}\" isn't a valid domain name. Use something like www.yourrestaurant.in.");
        return d;
    }

    /// <summary>Registrable root: www.spicegarden.co.in → spicegarden.co.in.</summary>
    private static string RootDomain(string domain)
    {
        var labels = domain.Split('.');
        if (labels.Length <= 2) return domain;
        var lastTwo = string.Join('.', labels[^2..]);
        return MultiLabelSuffixes.Contains(lastTwo) && labels.Length >= 3 ? string.Join('.', labels[^3..]) : lastTwo;
    }

    /// <summary>"_tabverse-verify" + "www.spicegarden.in" → "_tabverse-verify.www.spicegarden.in".</summary>
    private static string RecordName(string prefix, string domain) => $"{prefix}.{domain}";

    /// <summary>The Host field registrars ask for: the name without the root domain ("@" for the root itself).</summary>
    private static string RelativeHost(string fullName, string root) =>
        fullName == root ? "@" : fullName.EndsWith("." + root) ? fullName[..^(root.Length + 1)] : fullName;

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private async Task<Tenant> CurrentTenantAsync()
    {
        var tenantId = _tenantProvider.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");
        return await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");
    }
}
