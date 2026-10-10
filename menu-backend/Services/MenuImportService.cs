using menu_backend.Data;
using menu_backend.DTOs.Menu;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using static menu_backend.Helpers.ExcelImport;

namespace menu_backend.Services;

/// <summary>
/// Bulk menu import from an Excel sheet. One row per item; categories are created on the fly.
/// Existing items are matched by name (case-insensitive) and optionally updated. Nothing is deleted.
/// </summary>
public class MenuImportService : IMenuImportService
{
    public const int MaxRows = 2000;

    private static readonly ExcelColumn[] Columns =
    {
        new("Category", "Category *", new[] { "category", "categoryname", "section" }, Required: true, Width: 18),
        new("Name", "Item Name *", new[] { "itemname", "name", "item", "dish", "dishname" }, Required: true, Width: 28),
        new("Price", "Price *", new[] { "price", "rate", "mrp", "amount", "priceinr", "price₹" }, Required: true, Width: 10, NumberFormat: "0.00"),
        new("Description", "Description", new[] { "description", "desc", "details" }, Width: 50),
        new("Veg", "Veg (Yes/No)", new[] { "veg", "isveg", "vegnonveg", "type", "foodtype" }, Width: 14),
        new("Available", "Available (Yes/No)", new[] { "available", "isavailable", "instock", "active" }, Width: 18),
        new("PrepTime", "Prep Time (mins)", new[] { "preptime", "preptimemins", "preparationtime", "preparationtimeminutes", "preptimeminutes", "time" }, Width: 16),
        new("ImageUrl", "Image URL", new[] { "imageurl", "image", "photo", "photourl" }, Width: 30),
    };

    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public MenuImportService(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public byte[] BuildTemplate() => ExcelImport.BuildTemplate("Menu", Columns, "How to fill the menu sheet",
        new[]
        {
            "• One row per menu item. Columns marked * are required.",
            "• Category: categories that don't exist yet are created automatically.",
            "• Price: a number, e.g. 250 or 249.50 (no ₹ sign needed).",
            "• Veg: Yes / No (also accepts Veg / Non-Veg). Leave blank for No.",
            "• Available: Yes / No. Leave blank for Yes.",
            "• Prep Time: minutes, e.g. 15. Leave blank for 15.",
            "• Items whose name already exists on your menu can be updated instead of duplicated (option shown when uploading).",
            "  When updating, blank cells keep the item's current value.",
            "• Nothing is deleted: items not in the sheet stay as they are.",
            $"• Up to {MaxRows} rows per file. You'll see a preview before anything is saved.",
        },
        new[]
        {
            new object[] { "Starters", "Paneer Tikka", 260, "Cottage cheese marinated in spices, grilled in tandoor", "Yes", "Yes", 15, "" },
            new object[] { "Starters", "Chicken 65", 280, "Spicy deep-fried chicken", "No", "Yes", 15, "" },
            new object[] { "Main Course", "Dal Makhani", 220, "Slow-cooked black lentils with butter and cream", "Yes", "Yes", 20, "" },
            new object[] { "Breads", "Butter Naan", 50, "", "Yes", "Yes", 5, "" },
        });

    public async Task<MenuImportResult> ImportAsync(Stream xlsx, bool updateExisting, bool apply)
    {
        var rows = ReadRows(xlsx, "Menu", Columns, MaxRows, ParseRow);

        var categories = await _db.MenuCategories.ToListAsync();
        var items = await _db.MenuItems.ToListAsync();

        var categoryByName = categories.GroupBy(c => Key(c.Name)).ToDictionary(g => g.Key, g => g.First());
        var itemsByName = items.GroupBy(i => Key(i.Name)).ToDictionary(g => g.Key, g => g.ToList());

        // ── Validate & decide what happens to each row ──
        var seenInFile = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Category)) row.Errors.Add("Category is required.");
            else if (row.Category.Length > 150) row.Errors.Add("Category is longer than 150 characters.");

            if (string.IsNullOrWhiteSpace(row.Name)) row.Errors.Add("Item name is required.");
            else if (row.Name.Length > 200) row.Errors.Add("Item name is longer than 200 characters.");

            if (row.Price is null) { if (!row.Errors.Any(e => e.StartsWith("Price"))) row.Errors.Add("Price is required."); }
            else if (row.Price < 0) row.Errors.Add("Price cannot be negative.");
            else if (row.Price > 99_999_999) row.Errors.Add("Price is too large.");

            if (row.Description?.Length > 1000) row.Errors.Add("Description is longer than 1000 characters.");
            if (row.ImageUrl?.Length > 500) row.Errors.Add("Image URL is longer than 500 characters.");
            if (row.PreparationTimeMinutes is < 0 or > 600) row.Errors.Add("Prep time must be between 0 and 600 minutes.");

            if (!string.IsNullOrWhiteSpace(row.Name))
            {
                var key = Key(row.Name);
                if (seenInFile.TryGetValue(key, out var firstRow))
                    row.Errors.Add($"Duplicate of row {firstRow} — each item name can appear only once.");
                else
                    seenInFile[key] = row.RowNumber;

                if (row.Errors.Count == 0 && itemsByName.TryGetValue(key, out var matches))
                {
                    if (matches.Count > 1)
                        row.Errors.Add($"{matches.Count} items named \"{row.Name}\" already exist; rename them so the match is unambiguous.");
                    else
                    {
                        DescribeChanges(row, matches[0], categories);
                        row.Action = !updateExisting ? "Skip" : row.Changes.Count == 0 ? "Unchanged" : "Update";
                    }
                }
            }

            if (row.Errors.Count > 0) row.Action = "Error";
        }

        var result = new MenuImportResult
        {
            Rows = rows,
            NewCategories = rows
                .Where(r => r.Action is "Create" or "Update" && !categoryByName.ContainsKey(Key(r.Category)))
                .Select(r => r.Category.Trim())
                .DistinctBy(Key)
                .ToList()
        };
        result.Tally();

        if (!apply || result.CreateCount + result.UpdateCount == 0)
            return result;

        // ── Apply: one SaveChanges, so the import is all-or-nothing ──
        var tenantId = _tenantProvider.TenantId!;
        var nextCategorySort = categories.Count == 0 ? 0 : categories.Max(c => c.SortOrder) + 1;
        foreach (var name in result.NewCategories)
        {
            var category = new MenuCategory { TenantId = tenantId, Name = name, SortOrder = nextCategorySort++, IsActive = true };
            _db.MenuCategories.Add(category);
            categoryByName[Key(name)] = category;
        }

        var nextItemSort = items.GroupBy(i => i.CategoryId).ToDictionary(g => g.Key, g => g.Max(i => i.SortOrder) + 1);

        foreach (var row in rows.Where(r => r.Action is "Create" or "Update"))
        {
            var category = categoryByName[Key(row.Category)];

            if (row.Action == "Update")
            {
                // Matched by name; keep the existing spelling. Only fields with a value in the sheet are written.
                var item = itemsByName[Key(row.Name)].Single();
                item.CategoryId = category.Id;
                item.Price = row.Price!.Value;
                if (row.Provided.Contains("Description")) item.Description = row.Description;
                if (row.Provided.Contains("Veg")) item.IsVeg = row.IsVeg;
                if (row.Provided.Contains("Available")) item.IsAvailable = row.IsAvailable;
                if (row.Provided.Contains("PrepTime")) item.PreparationTimeMinutes = row.PreparationTimeMinutes;
                if (row.Provided.Contains("ImageUrl")) item.ImageUrl = row.ImageUrl;
                item.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var sort = nextItemSort.GetValueOrDefault(category.Id);
                nextItemSort[category.Id] = sort + 1;
                _db.MenuItems.Add(new MenuItem
                {
                    TenantId = tenantId,
                    Name = row.Name.Trim(),
                    CategoryId = category.Id,
                    Price = row.Price!.Value,
                    Description = row.Description,
                    IsVeg = row.IsVeg,
                    IsAvailable = row.IsAvailable,
                    PreparationTimeMinutes = row.PreparationTimeMinutes,
                    ImageUrl = row.ImageUrl,
                    SortOrder = sort
                });
            }
        }

        await _db.SaveChangesAsync();
        result.Applied = true;
        return result;
    }

    private static MenuImportRow ParseRow(SheetRow s)
    {
        var r = new MenuImportRow
        {
            RowNumber = s.RowNumber,
            Category = s.Text("Category"),
            Name = s.Text("Name"),
            Description = s.TextOrNull("Description"),
            ImageUrl = s.TextOrNull("ImageUrl")
        };
        foreach (var field in new[] { "Description", "ImageUrl", "Veg", "Available", "PrepTime" })
            if (s.Has(field)) r.Provided.Add(field);

        var price = s.Decimal("Price", "Price", r.Errors);
        r.Price = price is null ? null : Math.Round(price.Value, 2);

        var veg = ParseVeg(s.Text("Veg"));
        if (veg is null) r.Errors.Add($"Veg must be Yes or No (got \"{s.Text("Veg")}\").");
        else r.IsVeg = veg.Value;

        var available = ParseYesNo(s.Text("Available"), defaultValue: true);
        if (available is null) r.Errors.Add($"Available must be Yes or No (got \"{s.Text("Available")}\").");
        else r.IsAvailable = available.Value;

        var prep = s.Decimal("PrepTime", "Prep time", r.Errors);
        if (prep != null) r.PreparationTimeMinutes = (int)Math.Round(prep.Value);

        return r;
    }

    private static bool? ParseVeg(string value)
    {
        var v = Key(value);
        if (v is "veg" or "vegetarian" or "v" or "green") return true;
        if (v is "nonveg" or "nonvegetarian" or "nv" or "red" or "egg") return false;
        return ParseYesNo(value, defaultValue: false);
    }

    private static void DescribeChanges(MenuImportRow row, MenuItem item, List<MenuCategory> categories)
    {
        // Blank cells keep the current value — show that value in the preview, too
        if (!row.Provided.Contains("Description")) row.Description = item.Description;
        if (!row.Provided.Contains("Veg")) row.IsVeg = item.IsVeg;
        if (!row.Provided.Contains("Available")) row.IsAvailable = item.IsAvailable;
        if (!row.Provided.Contains("PrepTime")) row.PreparationTimeMinutes = item.PreparationTimeMinutes;
        if (!row.Provided.Contains("ImageUrl")) row.ImageUrl = item.ImageUrl;

        var currentCategory = categories.FirstOrDefault(c => c.Id == item.CategoryId)?.Name ?? "";
        if (Key(currentCategory) != Key(row.Category))
            row.Changes.Add($"Category {currentCategory} → {row.Category.Trim()}");
        if (row.Price != item.Price)
            row.Changes.Add($"Price ₹{item.Price:0.##} → ₹{row.Price:0.##}");
        if (row.Description != item.Description)
            row.Changes.Add("Description updated");
        if (row.IsVeg != item.IsVeg)
            row.Changes.Add(row.IsVeg ? "Non-veg → Veg" : "Veg → Non-veg");
        if (row.IsAvailable != item.IsAvailable)
            row.Changes.Add(row.IsAvailable ? "Unavailable → Available" : "Available → Unavailable");
        if (row.PreparationTimeMinutes != item.PreparationTimeMinutes)
            row.Changes.Add($"Prep time {item.PreparationTimeMinutes} → {row.PreparationTimeMinutes} min");
        if (row.ImageUrl != item.ImageUrl)
            row.Changes.Add("Image updated");
    }
}
