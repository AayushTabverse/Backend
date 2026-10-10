using menu_backend.DTOs;
using menu_backend.Helpers;
using menu_backend.DTOs.Menu;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menuService;
    private readonly IMenuImportService _importService;

    public MenuController(IMenuService menuService, IMenuImportService importService)
    {
        _menuService = menuService;
        _importService = importService;
    }

    // ── Excel import ──

    [HttpGet("import/template")]
    [Authorize(Roles = Roles.Management)]
    public IActionResult DownloadImportTemplate()
    {
        return File(_importService.BuildTemplate(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "tabverse-menu-template.xlsx");
    }

    /// <summary>
    /// Upload an Excel sheet. With apply=false (default) nothing is saved and the parsed rows are
    /// returned for preview; with apply=true valid rows are imported and error rows skipped.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = Roles.Management)]
    [RequestSizeLimit(ExcelImport.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportMenu(IFormFile? file, [FromQuery] bool updateExisting = true, [FromQuery] bool apply = false)
    {
        if (ExcelImport.ValidateFile(file) is { } fileError)
            return BadRequest(ApiResponse.Fail(fileError));

        try
        {
            await using var stream = file!.OpenReadStream();
            var result = await _importService.ImportAsync(stream, updateExisting, apply);
            var message = result.Applied
                ? $"Imported {result.CreateCount} new and updated {result.UpdateCount} items."
                : null;
            return Ok(ApiResponse<MenuImportResult>.Ok(result, message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ── Categories ──

    [HttpPost("category")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var result = await _menuService.CreateCategoryAsync(request);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Category created."));
    }

    [HttpPut("category/{id}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var result = await _menuService.UpdateCategoryAsync(id, request);
            return Ok(ApiResponse<CategoryResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("category/{id}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            await _menuService.DeleteCategoryAsync(id);
            return Ok(ApiResponse.Ok("Category deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("categories")]
    [Authorize]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _menuService.GetCategoriesAsync();
        return Ok(ApiResponse<List<CategoryResponse>>.Ok(result));
    }

    // ── Items ──

    [HttpPost("item")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> CreateItem([FromBody] CreateMenuItemRequest request)
    {
        var result = await _menuService.CreateItemAsync(request);
        return Ok(ApiResponse<MenuItemResponse>.Ok(result, "Item created."));
    }

    [HttpPut("item/{id}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateMenuItemRequest request)
    {
        try
        {
            var result = await _menuService.UpdateItemAsync(id, request);
            return Ok(ApiResponse<MenuItemResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("item/{id}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        try
        {
            await _menuService.DeleteItemAsync(id);
            return Ok(ApiResponse.Ok("Item deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("item/{id}")]
    [Authorize]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var result = await _menuService.GetItemAsync(id);
        if (result == null) return NotFound(ApiResponse.Fail("Item not found."));
        return Ok(ApiResponse<MenuItemResponse>.Ok(result));
    }

    // ── Public menu (no auth needed for customer) ──

    /// <summary>
    /// Get full menu for a tenant (public - used by QR scan customers).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenu([FromQuery] string tenantId)
    {
        try
        {
            var result = await _menuService.GetFullMenuAsync(tenantId);
            return Ok(ApiResponse<FullMenuResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get menu by table ID (public - used by QR scan).
    /// </summary>
    [HttpGet("by-table/{tableId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenuByTable(Guid tableId)
    {
        try
        {
            var result = await _menuService.GetMenuByTableAsync(tableId);
            return Ok(ApiResponse<FullMenuResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    // ── Ingredient Mapping ──

    /// <summary>
    /// Get the inventory ingredients linked to a menu item.
    /// </summary>
    [HttpGet("item/{menuItemId}/ingredients")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> GetIngredients(Guid menuItemId)
    {
        var result = await _menuService.GetIngredientsAsync(menuItemId);
        return Ok(ApiResponse<List<MenuItemIngredientResponse>>.Ok(result));
    }

    /// <summary>
    /// Set (replace) the inventory ingredients for a menu item.
    /// </summary>
    [HttpPut("item/{menuItemId}/ingredients")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> SetIngredients(Guid menuItemId, [FromBody] SetMenuItemIngredientsRequest request)
    {
        try
        {
            var result = await _menuService.SetIngredientsAsync(menuItemId, request);
            return Ok(ApiResponse<List<MenuItemIngredientResponse>>.Ok(result, "Ingredients updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
