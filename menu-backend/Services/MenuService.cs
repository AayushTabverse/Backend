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

    public MenuService(AppDbContext db, ITenantProvider tenantProvider)
    {
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
            Price = request.Price,
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
        item.Price = request.Price;
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
            Categories = categories.Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
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

        return await GetFullMenuAsync(table.TenantId);
    }

    // ── Mappers ──

    private static MenuItemResponse MapItem(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
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
        ImageUrl = category.ImageUrl,
        SortOrder = category.SortOrder,
        IsActive = category.IsActive
    };
}
