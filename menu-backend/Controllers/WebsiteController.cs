using System.Security.Claims;
using menu_backend.DTOs;
using menu_backend.Helpers;
using menu_backend.DTOs.Website;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebsiteController : ControllerBase
{
    private readonly IWebsiteService _websiteService;
    private readonly ISubdomainService _subdomains;
    private readonly ICustomDomainService _customDomains;
    private readonly IConfiguration _config;

    public WebsiteController(IWebsiteService websiteService, ISubdomainService subdomains,
        ICustomDomainService customDomains, IConfiguration config)
    {
        _websiteService = websiteService;
        _subdomains = subdomains;
        _customDomains = customDomains;
        _config = config;
    }

    /// <summary>
    /// Which restaurant does this host belong to? name.tabverse.in (subdomain) or a verified custom
    /// domain like www.spicegarden.in. Public: the website calls it on load from the restaurant's own host.
    /// </summary>
    [HttpGet("resolve-host/{host}")]
    [AllowAnonymous]
    [EnableCors("PublicSite")]
    public async Task<IActionResult> ResolveHost(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        var siteDomain = (_config["App:SiteDomain"] ?? "tabverse.in").ToLowerInvariant();

        // name.tabverse.in (one label in front of the site domain) is a Tabverse subdomain; anything else is a custom domain
        var prefix = host.EndsWith("." + siteDomain) ? host[..^(siteDomain.Length + 1)] : null;
        var tenantId = prefix != null && !prefix.Contains('.')
            ? await _subdomains.ResolveTenantIdAsync(prefix)
            : await _customDomains.ResolveTenantIdAsync(host);

        return tenantId == null
            ? NotFound(new { message = "No restaurant website at this address." })
            : Ok(new { tenantId, host });
    }

    /// <summary>
    /// Get website content for the current tenant (admin).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> GetWebsiteContent()
    {
        try
        {
            var result = await _websiteService.GetWebsiteContentAsync();
            return Ok(ApiResponse<WebsiteContentResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get website content by tenantId (public — for subdomain websites).
    /// </summary>
    [HttpGet("public/{tenantId}")]
    [AllowAnonymous]
    [EnableCors("PublicSite")]
    public async Task<IActionResult> GetPublicWebsiteContent(string tenantId)
    {
        try
        {
            var result = await _websiteService.GetWebsiteContentByTenantIdAsync(tenantId);

            // Unpublished sites are hidden from visitors; the restaurant's own owner/manager can still preview
            if (!result.IsPublished && !IsOwnStaffPreview(tenantId))
                return NotFound(ApiResponse.Fail("This website isn't published yet.", new List<string> { "not_published" }));

            return Ok(ApiResponse<WebsiteContentResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update website content for the current tenant.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> UpdateWebsiteContent([FromBody] UpdateWebsiteContentRequest request)
    {
        try
        {
            var result = await _websiteService.UpdateWebsiteContentAsync(request);
            return Ok(ApiResponse<WebsiteContentResponse>.Ok(result, "Website content updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    private bool IsOwnStaffPreview(string tenantId) =>
        User.Identity?.IsAuthenticated == true
        && User.FindFirstValue("tenant_id") == tenantId
        && (User.IsInRole("RestaurantAdmin") || User.IsInRole("SuperAdmin") || User.IsInRole("Manager"));
}
