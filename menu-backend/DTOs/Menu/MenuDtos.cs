using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Menu;

// ── Category DTOs ──

public class CreateCategoryRequest
{
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
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

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
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

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
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
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
    public List<CategoryResponse> Categories { get; set; } = new();
}
