using menu_backend.DTOs.Menu;

namespace menu_backend.Services.Interfaces;

public interface IMenuService
{
    // Categories
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryResponse> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request);
    Task DeleteCategoryAsync(Guid id);
    Task<List<CategoryResponse>> GetCategoriesAsync();

    // Items
    Task<MenuItemResponse> CreateItemAsync(CreateMenuItemRequest request);
    Task<MenuItemResponse> UpdateItemAsync(Guid id, UpdateMenuItemRequest request);
    Task DeleteItemAsync(Guid id);
    Task<MenuItemResponse?> GetItemAsync(Guid id);

    // Full menu (for customer view)
    Task<FullMenuResponse> GetFullMenuAsync(string tenantId);
    Task<FullMenuResponse> GetMenuByTableAsync(Guid tableId);
}
