using menu_backend.DTOs.Inventory;

namespace menu_backend.Services.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryItemResponse>> GetItemsAsync(string? category = null, bool? lowStockOnly = null);
    Task<InventoryItemResponse?> GetItemAsync(Guid id);
    Task<InventoryItemResponse> CreateItemAsync(CreateInventoryItemRequest request);
    Task<InventoryItemResponse> UpdateItemAsync(Guid id, UpdateInventoryItemRequest request);
    Task DeleteItemAsync(Guid id);
    Task<InventoryItemResponse> AdjustQuantityAsync(Guid id, AdjustQuantityRequest request, string? userName);
    Task<List<InventoryLogResponse>> GetLogsAsync(Guid? itemId = null, int days = 30);
    Task<InventorySummaryResponse> GetSummaryAsync();
    Task<List<string>> GetCategoriesAsync();

    /// <summary>
    /// Deduct inventory for all items in a completed order based on MenuItemIngredient mappings.
    /// </summary>
    Task DeductForOrderAsync(Guid orderId, string? changedBy = null);
}
