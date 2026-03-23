using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace menu_backend.Models;

/// <summary>
/// Stores the customizable website content for each tenant's public restaurant website.
/// Each tenant has exactly one WebsiteContent record.
/// </summary>
public class WebsiteContent : BaseEntity
{
    // ── Hero Section ──
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

    // ── About Section ──
    [MaxLength(200)]
    public string? AboutTitle { get; set; }

    [Column(TypeName = "text")]
    public string? AboutDescription { get; set; }

    [MaxLength(500)]
    public string? AboutImageUrl { get; set; }

    [MaxLength(200)]
    public string? ChefName { get; set; }

    [MaxLength(500)]
    public string? ChefImageUrl { get; set; }

    [MaxLength(500)]
    public string? ChefQuote { get; set; }

    // ── Specialties (3 feature cards) ──
    [MaxLength(100)]
    public string? Specialty1Title { get; set; }
    [MaxLength(300)]
    public string? Specialty1Description { get; set; }
    [MaxLength(50)]
    public string? Specialty1Icon { get; set; }

    [MaxLength(100)]
    public string? Specialty2Title { get; set; }
    [MaxLength(300)]
    public string? Specialty2Description { get; set; }
    [MaxLength(50)]
    public string? Specialty2Icon { get; set; }

    [MaxLength(100)]
    public string? Specialty3Title { get; set; }
    [MaxLength(300)]
    public string? Specialty3Description { get; set; }
    [MaxLength(50)]
    public string? Specialty3Icon { get; set; }

    // ── Gallery (up to 6 images stored as JSON array) ──
    [Column(TypeName = "text")]
    public string? GalleryImagesJson { get; set; }

    // ── Testimonials (stored as JSON array) ──
    [Column(TypeName = "text")]
    public string? TestimonialsJson { get; set; }

    // ── Operating Hours (stored as JSON) ──
    [Column(TypeName = "text")]
    public string? OperatingHoursJson { get; set; }

    // ── Theme / Appearance ──
    [MaxLength(20)]
    public string? PrimaryColor { get; set; } = "#e94560";

    [MaxLength(20)]
    public string? SecondaryColor { get; set; } = "#1a1a2e";

    [MaxLength(20)]
    public string? AccentColor { get; set; } = "#f39c12";

    [MaxLength(50)]
    public string? FontFamily { get; set; } = "Playfair Display";

    // ── SEO ──
    [MaxLength(200)]
    public string? MetaTitle { get; set; }

    [MaxLength(500)]
    public string? MetaDescription { get; set; }

    // ── Banner / Announcement ──
    [MaxLength(300)]
    public string? AnnouncementText { get; set; }

    public bool ShowAnnouncement { get; set; } = false;

    // ── Website Visibility ──
    public bool IsPublished { get; set; } = false;
}
