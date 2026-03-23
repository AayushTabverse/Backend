using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Website;

// ── Gallery Image ──
public class GalleryImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

// ── Testimonial ──
public class TestimonialDto
{
    public string Name { get; set; } = string.Empty;
    public string? Text { get; set; }
    public int Rating { get; set; } = 5;
    public string? AvatarUrl { get; set; }
}

// ── Operating Hours ──
public class OperatingHourDto
{
    public string Day { get; set; } = string.Empty;
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
    public bool IsClosed { get; set; } = false;
}

// ── Specialty Feature ──
public class SpecialtyDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

// ── Website Content Response (public & admin) ──
public class WebsiteContentResponse
{
    // Restaurant Info (from Tenant)
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? GoogleMapsUrl { get; set; }

    // Hero
    public string? HeroTitle { get; set; }
    public string? HeroSubtitle { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? HeroCtaText { get; set; }
    public string? HeroCtaLink { get; set; }

    // About
    public string? AboutTitle { get; set; }
    public string? AboutDescription { get; set; }
    public string? AboutImageUrl { get; set; }
    public string? ChefName { get; set; }
    public string? ChefImageUrl { get; set; }
    public string? ChefQuote { get; set; }

    // Specialties
    public List<SpecialtyDto> Specialties { get; set; } = new();

    // Gallery
    public List<GalleryImageDto> GalleryImages { get; set; } = new();

    // Testimonials
    public List<TestimonialDto> Testimonials { get; set; } = new();

    // Operating Hours
    public List<OperatingHourDto> OperatingHours { get; set; } = new();

    // Theme
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? FontFamily { get; set; }

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Banner
    public string? AnnouncementText { get; set; }
    public bool ShowAnnouncement { get; set; }

    // Publish status
    public bool IsPublished { get; set; }

    // Menu categories with items (for website display)
    public List<MenuCategoryWebDto>? MenuHighlights { get; set; }
}

// ── Lightweight menu DTO for website ──
public class MenuCategoryWebDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<MenuItemWebDto> Items { get; set; } = new();
}

public class MenuItemWebDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVeg { get; set; }
    public bool IsAvailable { get; set; }
}

// ── Update Request ──
public class UpdateWebsiteContentRequest
{
    // Hero
    [MaxLength(200)]
    public string? HeroTitle { get; set; }
    [MaxLength(500)]
    public string? HeroSubtitle { get; set; }
    [MaxLength(500)]
    public string? HeroImageUrl { get; set; }
    [MaxLength(100)]
    public string? HeroCtaText { get; set; }
    [MaxLength(500)]
    public string? HeroCtaLink { get; set; }

    // About
    [MaxLength(200)]
    public string? AboutTitle { get; set; }
    [MaxLength(2000)]
    public string? AboutDescription { get; set; }
    [MaxLength(500)]
    public string? AboutImageUrl { get; set; }
    [MaxLength(200)]
    public string? ChefName { get; set; }
    [MaxLength(500)]
    public string? ChefImageUrl { get; set; }
    [MaxLength(500)]
    public string? ChefQuote { get; set; }

    // Specialties
    public List<SpecialtyDto>? Specialties { get; set; }

    // Gallery
    public List<GalleryImageDto>? GalleryImages { get; set; }

    // Testimonials
    public List<TestimonialDto>? Testimonials { get; set; }

    // Operating Hours
    public List<OperatingHourDto>? OperatingHours { get; set; }

    // Theme
    [MaxLength(20)]
    public string? PrimaryColor { get; set; }
    [MaxLength(20)]
    public string? SecondaryColor { get; set; }
    [MaxLength(20)]
    public string? AccentColor { get; set; }
    [MaxLength(50)]
    public string? FontFamily { get; set; }

    // SEO
    [MaxLength(200)]
    public string? MetaTitle { get; set; }
    [MaxLength(500)]
    public string? MetaDescription { get; set; }

    // Banner
    [MaxLength(300)]
    public string? AnnouncementText { get; set; }
    public bool? ShowAnnouncement { get; set; }

    // Publish
    public bool? IsPublished { get; set; }
}
