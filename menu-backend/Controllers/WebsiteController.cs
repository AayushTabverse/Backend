using menu_backend.DTOs;
using menu_backend.DTOs.Website;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebsiteController : ControllerBase
{
    private readonly IWebsiteService _websiteService;

    public WebsiteController(IWebsiteService websiteService)
    {
        _websiteService = websiteService;
    }

    /// <summary>
    /// Get website content for the current tenant (admin).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
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
    public async Task<IActionResult> GetPublicWebsiteContent(string tenantId)
    {
        try
        {
            var result = await _websiteService.GetWebsiteContentByTenantIdAsync(tenantId);
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
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
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
}
