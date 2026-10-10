using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Website;

// ── Subdomain Availability Check ──
public class CheckSubdomainRequest
{
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$",
        ErrorMessage = "Subdomain can only contain lowercase letters, numbers, and hyphens.")]
    public string Subdomain { get; set; } = string.Empty;
}

public class CheckSubdomainResponse
{
    public string Subdomain { get; set; } = string.Empty;
    public string FullDomain { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? Message { get; set; }
}

// ── Claim / Publish Subdomain ──
public class ClaimSubdomainRequest
{
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$",
        ErrorMessage = "Subdomain can only contain lowercase letters, numbers, and hyphens.")]
    public string Subdomain { get; set; } = string.Empty;
}

public class SubdomainResponse
{
    public string? Subdomain { get; set; }
    public string? FullDomain { get; set; }
    public bool IsActive { get; set; }
    public string? DnsStatus { get; set; }
    public string? Message { get; set; }
}

// ── Subdomain Suggestions ──
public class SubdomainSuggestionsResponse
{
    public List<SubdomainSuggestion> Suggestions { get; set; } = new();
}

public class SubdomainSuggestion
{
    public string Subdomain { get; set; } = string.Empty;
    public string FullDomain { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

// ── Website status (what the owner sees after choosing a subdomain and publishing) ──

/// <summary>
/// One honest, owner-facing status for the restaurant website.
/// State is one of: not_started, published_no_domain, unpublished, dns_not_configured,
/// dns_failed, connecting, unreachable, live.
/// </summary>
public class WebsiteStatusResponse
{
    public string State { get; set; } = "not_started";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string? Subdomain { get; set; }
    /// <summary>Public address of the site (subdomain URL when one is set).</summary>
    public string? Url { get; set; }
    /// <summary>Owner preview path in this app, e.g. /website/{tenantId}.</summary>
    public string PreviewPath { get; set; } = string.Empty;

    public bool IsPublished { get; set; }
    public List<WebsiteStatusStep> Steps { get; set; } = new();

    /// <summary>True when re-running the DNS step may help.</summary>
    public bool CanRetryDns { get; set; }
    /// <summary>True while the site is still coming up; the editor re-checks automatically.</summary>
    public bool ShouldRecheck { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class WebsiteStatusStep
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>done, pending, failed or todo.</summary>
    public string Status { get; set; } = "todo";
    public string? Detail { get; set; }
}

public class PublishWebsiteRequest
{
    [Required]
    [MaxLength(63)]
    [RegularExpression(@"^[a-z0-9]([a-z0-9\-]{0,61}[a-z0-9])?$",
        ErrorMessage = "Subdomain can only contain lowercase letters, numbers, and hyphens.")]
    public string Subdomain { get; set; } = string.Empty;
}

// ── Bring-your-own domain ──

public class ConnectCustomDomainRequest
{
    // Validated in CustomDomainService so the owner gets a friendly message
    [MaxLength(300)]
    public string Domain { get; set; } = string.Empty;
}

/// <summary>A DNS record the owner must add at their domain provider, with its live check result.</summary>
public class DnsRecordInstruction
{
    /// <summary>ownership, pointing or hosting.</summary>
    public string Purpose { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    /// <summary>Host/Name field as most registrars expect it (relative to the root domain; "@" = root).</summary>
    public string Host { get; set; } = string.Empty;
    /// <summary>Fully-qualified record name, for providers that want the whole name.</summary>
    public string FullName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    /// <summary>ok, missing or wrong.</summary>
    public string Status { get; set; } = "missing";
    /// <summary>What we found instead, when Status is wrong.</summary>
    public string? Found { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// State: none, dns_pending, activating, unpublished, live.
/// </summary>
public class CustomDomainStatusResponse
{
    public string State { get; set; } = "none";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Url { get; set; }
    /// <summary>True for a root domain (spicegarden.in) rather than a subdomain (www.spicegarden.in).</summary>
    public bool IsApex { get; set; }
    public string? RootDomain { get; set; }
    public List<DnsRecordInstruction> Records { get; set; } = new();
    public List<WebsiteStatusStep> Steps { get; set; } = new();
    public bool ShouldRecheck { get; set; }
    public DateTime CheckedAt { get; set; }
}
