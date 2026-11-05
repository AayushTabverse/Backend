using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Menu;

/// <summary>A dish or category name/description in one language.</summary>
public class MenuText
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

// ── Category DTOs ──

public class CreateCategoryRequest
{
    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int SortOrder { get; set; } = 0;
}

public class UpdateCategoryRequest
{
    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CategoryResponse
{
    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<MenuItemResponse> Items { get; set; } = new();
}

// ── Menu Item DTOs ──

public class CreateMenuItemRequest
{
    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

    /// <summary>Packing charge per unit when ordered as parcel/takeaway.</summary>
    [Range(0, 10000)]
    public decimal PackagingCharge { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsVeg { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    [MaxLength(500)]
    public string? ARModelUrl { get; set; }

    public int PreparationTimeMinutes { get; set; } = 15;

    [Required]
    public Guid CategoryId { get; set; }

    public List<CreateModifierRequest>? Modifiers { get; set; }
}

public class UpdateMenuItemRequest
{
    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

    /// <summary>Packing charge per unit when ordered as parcel/takeaway.</summary>
    [Range(0, 10000)]
    public decimal PackagingCharge { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsVeg { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? ARModelUrl { get; set; }

    public int PreparationTimeMinutes { get; set; } = 15;

    public Guid CategoryId { get; set; }
}

public class MenuItemResponse
{
    /// <summary>The restaurant's own (English) name, kept when Name is shown in another language.</summary>
    public string? BaseName { get; set; }

    /// <summary>Other languages: { "hi": { name, description }, "gu": { … } }.</summary>
    public Dictionary<string, MenuText>? Translations { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal PackagingCharge { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsVeg { get; set; }
    public int SortOrder { get; set; }
    public string? ARModelUrl { get; set; }
    public int PreparationTimeMinutes { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<ModifierResponse> Modifiers { get; set; } = new();
}

// ── Modifier DTOs ──

public class CreateModifierRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal AdditionalPrice { get; set; } = 0;
}

public class ModifierResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal AdditionalPrice { get; set; }
    public bool IsAvailable { get; set; }
}

// ── Full Menu Response ──

public class FullMenuResponse
{
    public string TenantId { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? GoogleMapsUrl { get; set; }

    /// <summary>Set when the menu was opened from a table's QR.</summary>
    public string? TableNumber { get; set; }
    /// <summary>Set when that table is merged into another: orders are only taken on the main table.</summary>
    public string? MergedIntoTableNumber { get; set; }

    /// <summary>Customers may order takeaway from the counter QR.</summary>
    public bool TakeawayEnabled { get; set; }
    /// <summary>Flat packing fee added to each takeaway order.</summary>
    public decimal TakeawayPackingFee { get; set; }

    /// <summary>False outside opening hours (when the restaurant locks QR ordering): browse only.</summary>
    public bool OrderingOpen { get; set; } = true;
    public string? ClosedMessage { get; set; }
    /// <summary>Next opening, restaurant-local ISO time.</summary>
    public string? OpensAt { get; set; }
    /// <summary>Orders need the phone's location (only accepted from inside the restaurant).</summary>
    public bool LocationRequired { get; set; }

    public List<CategoryResponse> Categories { get; set; } = new();
}

// ── Ingredient Mapping DTOs ──

public class SetMenuItemIngredientsRequest
{
    [Required]
    public List<IngredientMapping> Ingredients { get; set; } = new();
}

public class IngredientMapping
{
    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>Quantity of inventory item consumed per 1 unit of this menu item</summary>
    [Required]
    public decimal QuantityUsed { get; set; }
}

public class MenuItemIngredientResponse
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string InventoryItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityUsed { get; set; }
    public decimal CurrentStock { get; set; }
}

public class PopularItemResponse
{
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
}
