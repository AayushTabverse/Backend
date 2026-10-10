using menu_backend.DTOs.Website;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

/// <summary>Bring-your-own domain for the restaurant website (owner only).</summary>
[ApiController]
[Route("api/custom-domain")]
[Authorize(Roles = Roles.Owner)]
public class CustomDomainController : ControllerBase
{
    private readonly ICustomDomainService _customDomains;

    public CustomDomainController(ICustomDomainService customDomains)
    {
        _customDomains = customDomains;
    }

    /// <summary>DNS records to add (each checked live) and the setup status. refresh=true skips the short cache.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] bool refresh = false)
    {
        try { return Ok(await _customDomains.GetStatusAsync(refresh)); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Connect (or replace) the restaurant's own domain.</summary>
    [HttpPut]
    public async Task<IActionResult> Connect([FromBody] ConnectCustomDomainRequest request)
    {
        try { return Ok(await _customDomains.ConnectAsync(request.Domain)); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Disconnect the restaurant's own domain.</summary>
    [HttpDelete]
    public async Task<IActionResult> Remove()
    {
        try
        {
            await _customDomains.RemoveAsync();
            return Ok(new { message = "Domain disconnected." });
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
