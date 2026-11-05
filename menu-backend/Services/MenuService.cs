using menu_backend.Helpers;
using menu_backend.Data;
using menu_backend.DTOs.Menu;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class MenuService : IMenuService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    private readonly OrderingAvailability _ordering;
    public MenuService(AppDbContext db, ITenantProvider tenantProvider, OrderingAvailability ordering)
    {
        _ordering = ordering;
        _db = db;
        _tenantProvider = tenantProvider;
    }

    // ── Categories ──

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var category = new MenuCategory
        {
            TenantId = _tenantProvider.TenantId!,
            Name = request.Name,
            Description = request.Description,
            TranslationsJson = MenuTranslations.Serialize(request.Translations),
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder
        };

        _db.MenuCategories.Add(category);
        await _db.SaveChangesAsync();

        return MapCategory(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _db.MenuCategories.FindAsync(id)
            ?? throw new KeyNotFoundException("Category not found.");

        category.Name = request.Name;
        category.Description = request.Description;
        if (request.Translations != null) category.TranslationsJson = MenuTranslations.Serialize(request.Translations);
        category.ImageUrl = request.ImageUrl;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapCategory(category);
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var category = await _db.MenuCategories.FindAsync(id)
            ?? throw new KeyNotFoundException("Category not found.");

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<PopularItemResponse>> GetPopularItemsAsync(int days, int limit)
    {
        var tenantId = _tenantProvider.TenantId!;
        var since = DateTime.UtcNow.AddDays(-days);
        return await _db.OrderItems
            .Where(i => i.TenantId == tenantId && i.Order!.CreatedAt >= since && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.MenuItemId)
            .Select(g => new PopularItemResponse { MenuItemId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(p => p.Quantity)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync()
    {
        return await _db.MenuCategories
            .Include(c => c.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Modifiers.Where(m => !m.IsDeleted))
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Translations = MenuTranslations.Parse(c.TranslationsJson),
                ImageUrl = c.ImageUrl,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                Items = c.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => MapItem(i))
                    .ToList()
            })
            .ToListAsync();
    }

    // ── Items ──

    public async Task<MenuItemResponse> CreateItemAsync(CreateMenuItemRequest request)
    {
        var item = new MenuItem
        {
            TenantId = _tenantProvider.TenantId!,
            Name = request.Name,
            Description = request.Description,
            TranslationsJson = MenuTranslations.Serialize(request.Translations),
            Price = request.Price,
            PackagingCharge = request.PackagingCharge,
            ImageUrl = request.ImageUrl,
            IsAvailable = request.IsAvailable,
            IsVeg = request.IsVeg,
            SortOrder = request.SortOrder,
            ARModelUrl = request.ARModelUrl,
            PreparationTimeMinutes = request.PreparationTimeMinutes,
            CategoryId = request.CategoryId,
        };

        if (request.Modifiers?.Any() == true)
        {
            foreach (var mod in request.Modifiers)
            {
                item.Modifiers.Add(new MenuItemModifier
                {
                    TenantId = _tenantProvider.TenantId!,
                    Name = mod.Name,
                    AdditionalPrice = mod.AdditionalPrice
                });
            }
        }

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        return MapItem(item);
    }

    public async Task<MenuItemResponse> UpdateItemAsync(Guid id, UpdateMenuItemRequest request)
    {
        var item = await _db.MenuItems
            .Include(i => i.Modifiers)
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException("Menu item not found.");

        item.Name = request.Name;
        item.Description = request.Description;
        if (request.Translations != null) item.TranslationsJson = MenuTranslations.Serialize(request.Translations);
        item.Price = request.Price;
        item.PackagingCharge = request.PackagingCharge;
        item.ImageUrl = request.ImageUrl;
        item.IsAvailable = request.IsAvailable;
        item.IsVeg = request.IsVeg;
        item.SortOrder = request.SortOrder;
        item.ARModelUrl = request.ARModelUrl;
        item.PreparationTimeMinutes = request.PreparationTimeMinutes;
        item.CategoryId = request.CategoryId;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapItem(item);
    }

    public async Task DeleteItemAsync(Guid id)
    {
        var item = await _db.MenuItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Menu item not found.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<MenuItemResponse?> GetItemAsync(Guid id)
    {
        var item = await _db.MenuItems
            .Include(i => i.Modifiers)
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id);

        return item == null ? null : MapItem(item);
    }

    // ── Full Menu ──

    public async Task<FullMenuResponse> GetFullMenuAsync(string tenantId)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Restaurant not found.");

        var categories = await _db.MenuCategories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .Include(c => c.Items.Where(i => !i.IsDeleted && i.IsAvailable))
                .ThenInclude(i => i.Modifiers.Where(m => !m.IsDeleted && m.IsAvailable))
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var ordering = await _ordering.GetStatusAsync(tenant);

        return new FullMenuResponse
        {
            TenantId = tenantId,
            RestaurantName = tenant.Name,
            LogoUrl = tenant.LogoUrl,
            InstagramUrl = tenant.InstagramUrl,
            FacebookUrl = tenant.FacebookUrl,
            TwitterUrl = tenant.TwitterUrl,
            WebsiteUrl = tenant.WebsiteUrl,
            GoogleMapsUrl = tenant.GoogleMapsUrl,
            TakeawayEnabled = tenant.TakeawayEnabled,
            TakeawayPackingFee = tenant.TakeawayPackingFee,
            OrderingOpen = ordering.Open,
            ClosedMessage = ordering.ClosedMessage,
            OpensAt = ordering.OpensAt,
            LocationRequired = OrderingAvailability.LocationCheckOn(tenant),
            Categories = categories.Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Translations = MenuTranslations.Parse(c.TranslationsJson),
                ImageUrl = c.ImageUrl,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                Items = c.Items.OrderBy(i => i.SortOrder).Select(i => MapItem(i)).ToList()
            }).ToList()
        };
    }

    public async Task<FullMenuResponse> GetMenuByTableAsync(Guid tableId)
    {
        var table = await _db.Tables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tableId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Table not found.");

        var menu = await GetFullMenuAsync(table.TenantId);
        menu.TableNumber = table.TableNumber;
        if (table.MergedIntoTableId != null)
        {
            menu.MergedIntoTableNumber = await _db.Tables
                .IgnoreQueryFilters()
                .Where(t => t.Id == table.MergedIntoTableId)
                .Select(t => t.TableNumber)
                .FirstOrDefaultAsync();
        }
        return menu;
    }

    // ── Translations ──

    public async Task<AutoTranslateResult> AutoTranslateAsync(bool overwrite, MenuTranslator translator)
    {
        var categories = await _db.MenuCategories.ToListAsync();
        var items = await _db.MenuItems.ToListAsync();

        // Rows to translate, keyed "c:{id}" / "i:{id}"
        var rows = categories.Select(c => (Key: $"c:{c.Id}", c.Name, c.Description, Json: c.TranslationsJson, Set: (Action<string?>)(j => c.TranslationsJson = j)))
            .Concat(items.Select(i => (Key: $"i:{i.Id}", i.Name, i.Description, Json: i.TranslationsJson, Set: (Action<string?>)(j => i.TranslationsJson = j))))
            .Where(r => overwrite || MenuTranslations.Languages.Any(l => !MenuTranslations.Parse(r.Json).ContainsKey(l)))
            .ToList();
        if (rows.Count == 0) return new AutoTranslateResult(0, "", 0, 0);

        var changed = 0;
        int inputTokens = 0, outputTokens = 0;
        var model = "";
        foreach (var batch in rows.Chunk(25))
        {
            Dictionary<string, Dictionary<string, MenuText>> result;
            try
            {
                var translated = await translator.TranslateAsync(
                    batch.ToDictionary(r => r.Key, r => new MenuText { Name = r.Name, Description = r.Description }),
                    MenuTranslations.Languages);
                result = translated.Texts;
                model = translated.Model;
                inputTokens += translated.InputTokens;
                outputTokens += translated.OutputTokens;
            }
            catch (Exception ex)
            {
                if (changed > 0) await _db.SaveChangesAsync();
                throw new InvalidOperationException($"Auto-translate stopped after {changed} entries: {ex.Message}");
            }

            foreach (var row in batch)
            {
                if (!result.TryGetValue(row.Key, out var byLang)) continue;
                var existing = MenuTranslations.Parse(row.Json);
                foreach (var (lang, text) in byLang)
                {
                    // Never show customers a description the restaurant didn't write
                    if (string.IsNullOrWhiteSpace(row.Description)) text.Description = null;
                    if (overwrite || !existing.ContainsKey(lang)) existing[lang] = text;
                }
                row.Set(MenuTranslations.Serialize(existing));
                changed++;
            }
        }

        await _db.SaveChangesAsync();
        return new AutoTranslateResult(changed, model, inputTokens, outputTokens);
    }

    // ── Mappers ──

    private static MenuItemResponse MapItem(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Translations = MenuTranslations.Parse(item.TranslationsJson),
        Price = item.Price,
        PackagingCharge = item.PackagingCharge,
        ImageUrl = item.ImageUrl,
        IsAvailable = item.IsAvailable,
        IsVeg = item.IsVeg,
        SortOrder = item.SortOrder,
        ARModelUrl = item.ARModelUrl,
        PreparationTimeMinutes = item.PreparationTimeMinutes,
        CategoryId = item.CategoryId,
        CategoryName = item.Category?.Name,
        Modifiers = item.Modifiers
            .Where(m => !m.IsDeleted)
            .Select(m => new ModifierResponse
            {
                Id = m.Id,
                Name = m.Name,
                AdditionalPrice = m.AdditionalPrice,
                IsAvailable = m.IsAvailable
            }).ToList()
    };

    private static CategoryResponse MapCategory(MenuCategory category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        Translations = MenuTranslations.Parse(category.TranslationsJson),
        ImageUrl = category.ImageUrl,
        SortOrder = category.SortOrder,
        IsActive = category.IsActive
    };

    // ── Ingredient Mapping ──

    public async Task<List<MenuItemIngredientResponse>> GetIngredientsAsync(Guid menuItemId)
    {
        return await _db.MenuItemIngredients
            .Where(i => i.MenuItemId == menuItemId)
            .Include(i => i.InventoryItem)
            .Select(i => new MenuItemIngredientResponse
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                InventoryItemName = i.InventoryItem!.Name,
                Unit = i.InventoryItem.Unit.ToString(),
                QuantityUsed = i.QuantityUsed,
                CurrentStock = i.InventoryItem.CurrentQuantity
            })
            .ToListAsync();
    }

    public async Task<List<MenuItemIngredientResponse>> SetIngredientsAsync(Guid menuItemId, SetMenuItemIngredientsRequest request)
    {
        var item = await _db.MenuItems.FindAsync(menuItemId)
            ?? throw new KeyNotFoundException("Menu item not found.");

        // Remove existing ingredients
        var existing = await _db.MenuItemIngredients
            .Where(i => i.MenuItemId == menuItemId)
            .ToListAsync();
        _db.MenuItemIngredients.RemoveRange(existing);

        // Add new ingredients
        foreach (var ing in request.Ingredients)
        {
            _db.MenuItemIngredients.Add(new MenuItemIngredient
            {
                MenuItemId = menuItemId,
                InventoryItemId = ing.InventoryItemId,
                QuantityUsed = ing.QuantityUsed
            });
        }

        await _db.SaveChangesAsync();
        return await GetIngredientsAsync(menuItemId);
    }
}
