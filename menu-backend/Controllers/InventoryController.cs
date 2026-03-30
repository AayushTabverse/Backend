using System.Security.Claims;
using menu_backend.DTOs;
using menu_backend.DTOs.Inventory;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems([FromQuery] string? category, [FromQuery] bool? lowStockOnly)
    {
        var items = await _inventoryService.GetItemsAsync(category, lowStockOnly);
        return Ok(ApiResponse<List<InventoryItemResponse>>.Ok(items));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var item = await _inventoryService.GetItemAsync(id);
        if (item == null) return NotFound(ApiResponse.Fail("Item not found."));
        return Ok(ApiResponse<InventoryItemResponse>.Ok(item));
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem([FromBody] CreateInventoryItemRequest request)
    {
        try
        {
            var item = await _inventoryService.CreateItemAsync(request);
            return Ok(ApiResponse<InventoryItemResponse>.Ok(item, "Item created."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateInventoryItemRequest request)
    {
        try
        {
            var item = await _inventoryService.UpdateItemAsync(id, request);
            return Ok(ApiResponse<InventoryItemResponse>.Ok(item));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        try
        {
            await _inventoryService.DeleteItemAsync(id);
            return Ok(ApiResponse.Ok("Item deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("{id}/adjust")]
    public async Task<IActionResult> AdjustQuantity(Guid id, [FromBody] AdjustQuantityRequest request)
    {
        try
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var item = await _inventoryService.AdjustQuantityAsync(id, request, userName);
            return Ok(ApiResponse<InventoryItemResponse>.Ok(item, "Quantity adjusted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] Guid? itemId, [FromQuery] int days = 30)
    {
        var logs = await _inventoryService.GetLogsAsync(itemId, days);
        return Ok(ApiResponse<List<InventoryLogResponse>>.Ok(logs));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _inventoryService.GetSummaryAsync();
        return Ok(ApiResponse<InventorySummaryResponse>.Ok(summary));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _inventoryService.GetCategoriesAsync();
        return Ok(ApiResponse<List<string>>.Ok(categories));
    }
}
