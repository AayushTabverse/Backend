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
