using System.Text.Json;
using menu_backend.Data;
using menu_backend.DTOs.Website;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class WebsiteService : IWebsiteService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public WebsiteService(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<WebsiteContentResponse> GetWebsiteContentAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        return await GetWebsiteContentByTenantIdAsync(tenantId);
    }

    public async Task<WebsiteContentResponse> GetWebsiteContentByTenantIdAsync(string tenantId)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        // Try to get existing website content; auto-create with defaults if missing
        var content = await _db.WebsiteContents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted);

        if (content == null)
        {
            content = CreateDefaultContent(tenantId, tenant.Name);
            _db.WebsiteContents.Add(content);
            await _db.SaveChangesAsync();
        }

        // Fetch menu highlights (top categories with items)
        var menuCategories = await _db.MenuCategories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Take(6)
            .Include(c => c.Items.Where(i => !i.IsDeleted && i.IsAvailable).OrderBy(i => i.SortOrder).Take(6))
            .ToListAsync();

        return MapToResponse(tenant, content, menuCategories);
    }

    public async Task<WebsiteContentResponse> UpdateWebsiteContentAsync(UpdateWebsiteContentRequest request)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        var content = await _db.WebsiteContents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted);

        if (content == null)
        {
            content = CreateDefaultContent(tenantId, tenant.Name);
            _db.WebsiteContents.Add(content);
            await _db.SaveChangesAsync();
        }

        // Update fields
        if (request.HeroTitle != null) content.HeroTitle = request.HeroTitle;
        if (request.HeroSubtitle != null) content.HeroSubtitle = request.HeroSubtitle;
        if (request.HeroImageUrl != null) content.HeroImageUrl = request.HeroImageUrl;
        if (request.HeroCtaText != null) content.HeroCtaText = request.HeroCtaText;
        if (request.HeroCtaLink != null) content.HeroCtaLink = request.HeroCtaLink;
        if (request.AboutTitle != null) content.AboutTitle = request.AboutTitle;
        if (request.AboutDescription != null) content.AboutDescription = request.AboutDescription;
        if (request.AboutImageUrl != null) content.AboutImageUrl = request.AboutImageUrl;
        if (request.ChefName != null) content.ChefName = request.ChefName;
        if (request.ChefImageUrl != null) content.ChefImageUrl = request.ChefImageUrl;
        if (request.ChefQuote != null) content.ChefQuote = request.ChefQuote;
        if (request.PrimaryColor != null) content.PrimaryColor = request.PrimaryColor;
        if (request.SecondaryColor != null) content.SecondaryColor = request.SecondaryColor;
        if (request.AccentColor != null) content.AccentColor = request.AccentColor;
        if (request.FontFamily != null) content.FontFamily = request.FontFamily;
        if (request.MetaTitle != null) content.MetaTitle = request.MetaTitle;
        if (request.MetaDescription != null) content.MetaDescription = request.MetaDescription;
        if (request.AnnouncementText != null) content.AnnouncementText = request.AnnouncementText;
        if (request.ShowAnnouncement.HasValue) content.ShowAnnouncement = request.ShowAnnouncement.Value;
        if (request.IsPublished.HasValue) content.IsPublished = request.IsPublished.Value;

        // Specialties
        if (request.Specialties != null)
        {
            var specs = request.Specialties;
            content.Specialty1Title = specs.Count > 0 ? specs[0].Title : null;
            content.Specialty1Description = specs.Count > 0 ? specs[0].Description : null;
            content.Specialty1Icon = specs.Count > 0 ? specs[0].Icon : null;
            content.Specialty2Title = specs.Count > 1 ? specs[1].Title : null;
            content.Specialty2Description = specs.Count > 1 ? specs[1].Description : null;
            content.Specialty2Icon = specs.Count > 1 ? specs[1].Icon : null;
            content.Specialty3Title = specs.Count > 2 ? specs[2].Title : null;
            content.Specialty3Description = specs.Count > 2 ? specs[2].Description : null;
            content.Specialty3Icon = specs.Count > 2 ? specs[2].Icon : null;
        }

        // Gallery (JSON)
        if (request.GalleryImages != null)
        {
            content.GalleryImagesJson = JsonSerializer.Serialize(request.GalleryImages, _jsonOpts);
        }

        // Testimonials (JSON)
        if (request.Testimonials != null)
        {
            content.TestimonialsJson = JsonSerializer.Serialize(request.Testimonials, _jsonOpts);
        }

        // Operating Hours (JSON)
        if (request.OperatingHours != null)
        {
            content.OperatingHoursJson = JsonSerializer.Serialize(request.OperatingHours, _jsonOpts);
        }

        content.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var menuCategories = await _db.MenuCategories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Take(6)
            .Include(c => c.Items.Where(i => !i.IsDeleted && i.IsAvailable).OrderBy(i => i.SortOrder).Take(6))
            .ToListAsync();

        return MapToResponse(tenant, content, menuCategories);
    }

    // ── Helpers ──

    private static WebsiteContent CreateDefaultContent(string tenantId, string restaurantName) => new()
    {
        TenantId = tenantId,
        HeroTitle = $"Welcome to {restaurantName}",
        HeroSubtitle = "Experience culinary excellence with every bite. Fresh ingredients, authentic flavors, unforgettable moments.",
        HeroCtaText = "View Our Menu",
        AboutTitle = "Our Story",
        AboutDescription = $"At {restaurantName}, we believe that great food brings people together. Our chefs craft each dish with passion, using the finest locally-sourced ingredients to create an unforgettable dining experience that celebrates tradition and innovation.",
        Specialty1Title = "Farm to Table",
        Specialty1Description = "We source the freshest ingredients from local farms to ensure quality in every dish.",
        Specialty1Icon = "🥬",
        Specialty2Title = "Master Chefs",
        Specialty2Description = "Our award-winning chefs bring decades of culinary expertise to your plate.",
        Specialty2Icon = "👨‍🍳",
        Specialty3Title = "Warm Ambiance",
        Specialty3Description = "Enjoy your meal in our beautifully designed space, perfect for any occasion.",
        Specialty3Icon = "✨",
        OperatingHoursJson = JsonSerializer.Serialize(new List<OperatingHourDto>
        {
            new() { Day = "Monday", OpenTime = "11:00 AM", CloseTime = "10:00 PM" },
            new() { Day = "Tuesday", OpenTime = "11:00 AM", CloseTime = "10:00 PM" },
            new() { Day = "Wednesday", OpenTime = "11:00 AM", CloseTime = "10:00 PM" },
            new() { Day = "Thursday", OpenTime = "11:00 AM", CloseTime = "10:00 PM" },
            new() { Day = "Friday", OpenTime = "11:00 AM", CloseTime = "11:00 PM" },
            new() { Day = "Saturday", OpenTime = "10:00 AM", CloseTime = "11:00 PM" },
            new() { Day = "Sunday", OpenTime = "10:00 AM", CloseTime = "10:00 PM" }
        }, _jsonOpts),
        TestimonialsJson = JsonSerializer.Serialize(new List<TestimonialDto>
        {
            new() { Name = "Sarah M.", Text = "Absolutely stunning food and atmosphere! Every dish was a work of art. Will definitely come back.", Rating = 5 },
            new() { Name = "Rahul K.", Text = "Best dining experience in the city. The flavors are authentic and the service is impeccable.", Rating = 5 },
            new() { Name = "Emily R.", Text = "A hidden gem! The chef's special was extraordinary. Highly recommend for date nights.", Rating = 5 }
        }, _jsonOpts),
        PrimaryColor = "#e94560",
        SecondaryColor = "#1a1a2e",
        AccentColor = "#f39c12",
        FontFamily = "Playfair Display",
        IsPublished = false
    };

    private WebsiteContentResponse MapToResponse(Tenant tenant, WebsiteContent content, List<MenuCategory> menuCategories)
    {
        var response = new WebsiteContentResponse
        {
            RestaurantName = tenant.Name,
            LogoUrl = tenant.LogoUrl,
            Phone = tenant.Phone,
            Email = tenant.Email,
            Address = tenant.Address,
            InstagramUrl = tenant.InstagramUrl,
            FacebookUrl = tenant.FacebookUrl,
            TwitterUrl = tenant.TwitterUrl,
            WebsiteUrl = tenant.WebsiteUrl,
            GoogleMapsUrl = tenant.GoogleMapsUrl,

            HeroTitle = content.HeroTitle,
            HeroSubtitle = content.HeroSubtitle,
            HeroImageUrl = content.HeroImageUrl,
            HeroCtaText = content.HeroCtaText,
            HeroCtaLink = content.HeroCtaLink,

            AboutTitle = content.AboutTitle,
            AboutDescription = content.AboutDescription,
            AboutImageUrl = content.AboutImageUrl,
            ChefName = content.ChefName,
            ChefImageUrl = content.ChefImageUrl,
            ChefQuote = content.ChefQuote,

            PrimaryColor = content.PrimaryColor,
            SecondaryColor = content.SecondaryColor,
            AccentColor = content.AccentColor,
            FontFamily = content.FontFamily,

            MetaTitle = content.MetaTitle,
            MetaDescription = content.MetaDescription,
            AnnouncementText = content.AnnouncementText,
            ShowAnnouncement = content.ShowAnnouncement,
            IsPublished = content.IsPublished
        };

        // Specialties
        var specialties = new List<SpecialtyDto>();
        if (!string.IsNullOrEmpty(content.Specialty1Title))
            specialties.Add(new() { Title = content.Specialty1Title, Description = content.Specialty1Description, Icon = content.Specialty1Icon });
        if (!string.IsNullOrEmpty(content.Specialty2Title))
            specialties.Add(new() { Title = content.Specialty2Title, Description = content.Specialty2Description, Icon = content.Specialty2Icon });
        if (!string.IsNullOrEmpty(content.Specialty3Title))
            specialties.Add(new() { Title = content.Specialty3Title, Description = content.Specialty3Description, Icon = content.Specialty3Icon });
        response.Specialties = specialties;

        // Gallery
        if (!string.IsNullOrEmpty(content.GalleryImagesJson))
        {
            try { response.GalleryImages = JsonSerializer.Deserialize<List<GalleryImageDto>>(content.GalleryImagesJson, _jsonOpts) ?? new(); }
            catch { response.GalleryImages = new(); }
        }

        // Testimonials
        if (!string.IsNullOrEmpty(content.TestimonialsJson))
        {
            try { response.Testimonials = JsonSerializer.Deserialize<List<TestimonialDto>>(content.TestimonialsJson, _jsonOpts) ?? new(); }
            catch { response.Testimonials = new(); }
        }

        // Operating hours
        if (!string.IsNullOrEmpty(content.OperatingHoursJson))
        {
            try { response.OperatingHours = JsonSerializer.Deserialize<List<OperatingHourDto>>(content.OperatingHoursJson, _jsonOpts) ?? new(); }
            catch { response.OperatingHours = new(); }
        }

        // Menu highlights
        response.MenuHighlights = menuCategories.Select(c => new MenuCategoryWebDto
        {
            Id = c.Id.ToString(),
            Name = c.Name,
            ImageUrl = c.ImageUrl,
            Items = c.Items.Select(i => new MenuItemWebDto
            {
                Id = i.Id.ToString(),
                Name = i.Name,
                Description = i.Description,
                Price = i.Price,
                ImageUrl = i.ImageUrl,
                IsVeg = i.IsVeg,
                IsAvailable = i.IsAvailable
            }).ToList()
        }).ToList();

        return response;
    }
}
