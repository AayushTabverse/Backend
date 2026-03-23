using menu_backend.DTOs.Website;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubdomainController : ControllerBase
{
    private readonly ISubdomainService _subdomainService;

    public SubdomainController(ISubdomainService subdomainService)
    {
        _subdomainService = subdomainService;
    }

    /// <summary>
    /// Get subdomain suggestions based on restaurant name.
    /// </summary>
    [HttpGet("suggestions")]
    [Authorize(Roles = "SuperAdmin,RestaurantAdmin")]
    public async Task<IActionResult> GetSuggestions()
    {
        try
        {
            var result = await _subdomainService.GetSuggestionsAsync();
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Check if a subdomain is available.
    /// </summary>
    [HttpGet("check/{subdomain}")]
    [Authorize(Roles = "SuperAdmin,RestaurantAdmin")]
    public async Task<IActionResult> CheckAvailability(string subdomain)
    {
        try
        {
            var result = await _subdomainService.CheckAvailabilityAsync(subdomain);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    /// <summary>
    /// Claim a subdomain and create DNS record.
    /// </summary>
    [HttpPost("claim")]
    [Authorize(Roles = "SuperAdmin,RestaurantAdmin")]
    public async Task<IActionResult> ClaimSubdomain([FromBody] ClaimSubdomainRequest request)
    {
        try
        {
            var result = await _subdomainService.ClaimSubdomainAsync(request.Subdomain);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Release the current subdomain.
    /// </summary>
    [HttpDelete("release")]
    [Authorize(Roles = "SuperAdmin,RestaurantAdmin")]
    public async Task<IActionResult> ReleaseSubdomain()
    {
        try
        {
            var result = await _subdomainService.ReleaseSubdomainAsync();
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Get the current tenant's subdomain info.
    /// </summary>
    [HttpGet("current")]
    [Authorize(Roles = "SuperAdmin,RestaurantAdmin")]
    public async Task<IActionResult> GetCurrentSubdomain()
    {
        try
        {
            var result = await _subdomainService.GetCurrentSubdomainAsync();
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Resolve a subdomain to a tenant ID (public endpoint for routing).
    /// </summary>
    [HttpGet("resolve/{subdomain}")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolveSubdomain(string subdomain)
    {
        var tenantId = await _subdomainService.ResolveTenantIdAsync(subdomain);

        if (tenantId == null)
            return NotFound(new { message = "No restaurant found for this subdomain." });

        return Ok(new { tenantId, subdomain, fullDomain = $"{subdomain}.tabverse.in" });
    }
}
