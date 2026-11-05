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

    /// <summary>Best sellers by quantity over the last <paramref name="days"/> days (for the waiter's "Popular" tab).</summary>
    Task<List<PopularItemResponse>> GetPopularItemsAsync(int days, int limit);

    /// <summary>AI-translates names/descriptions missing a Hindi or Gujarati version. Returns how many rows changed.</summary>
    Task<AutoTranslateResult> AutoTranslateAsync(bool overwrite, MenuTranslator translator);

    // Ingredient mapping
    Task<List<MenuItemIngredientResponse>> GetIngredientsAsync(Guid menuItemId);
    Task<List<MenuItemIngredientResponse>> SetIngredientsAsync(Guid menuItemId, SetMenuItemIngredientsRequest request);
}

/// <summary>How many dishes/categories were translated, and the OpenAI tokens it took.</summary>
public record AutoTranslateResult(int Changed, string Model, int InputTokens, int OutputTokens);
