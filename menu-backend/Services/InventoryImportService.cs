using menu_backend.Data;
using menu_backend.DTOs.Inventory;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using static menu_backend.Helpers.ExcelImport;

namespace menu_backend.Services;

/// <summary>
/// Bulk inventory import from Excel. Items are matched by name (case-insensitive) and optionally
/// updated; quantity changes are written to the inventory log like a manual adjustment.
/// </summary>
public class InventoryImportService : IInventoryImportService
{
    public const int MaxRows = 2000;

    private static readonly ExcelColumn[] Columns =
    {
        new("Name", "Item Name *", new[] { "itemname", "name", "item", "ingredient", "material", "product" }, Required: true, Width: 26),
        new("Unit", "Unit *", new[] { "unit", "units", "uom", "unitofmeasure", "measure" }, Required: true, Width: 10),
        new("CurrentQuantity", "Current Quantity", new[] { "currentquantity", "quantity", "qty", "stock", "currentstock", "instock", "openingstock" }, Width: 16),
        new("MinimumQuantity", "Minimum Quantity", new[] { "minimumquantity", "minquantity", "minqty", "minimum", "minstock", "reorderlevel", "lowstockalert", "alertbelow" }, Width: 17),
        new("CostPerUnit", "Cost per Unit (₹)", new[] { "costperunit", "cost", "unitcost", "rate", "price", "purchaseprice" }, Width: 17, NumberFormat: "0.00"),
        new("Category", "Category", new[] { "category", "type", "group" }, Width: 16),
        new("Supplier", "Supplier", new[] { "supplier", "vendor", "suppliername", "vendorname" }, Width: 20),
        new("SupplierContact", "Supplier Contact", new[] { "suppliercontact", "contact", "suppliermobile", "supplierphone", "vendorcontact", "phone", "mobile" }, Width: 18),
        new("Description", "Description", new[] { "description", "desc", "notes", "details" }, Width: 30),
    };

    private static readonly Dictionary<string, InventoryUnit> UnitAliases = BuildUnitAliases();

    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public InventoryImportService(AppDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public byte[] BuildTemplate() => ExcelImport.BuildTemplate("Inventory", Columns, "How to fill the inventory sheet",
        new[]
        {
            "• One row per stock item. Columns marked * are required.",
            $"• Unit: one of {string.Join(", ", Enum.GetNames<InventoryUnit>())} (also accepts kg/kgs, g/gm, l/ltr, ml, pcs/nos, pkt…).",
            "• Current Quantity: stock you have now. Leave blank for 0 on new items.",
            "• Minimum Quantity: you get a low-stock alert when stock falls to this level.",
            "• Cost per Unit: purchase cost in ₹ per unit (used for inventory value).",
            "• Items whose name already exists can be updated instead of duplicated (option shown when uploading).",
            "  When updating, blank cells keep the current value. Quantity changes are recorded in the Activity Log.",
            "• Nothing is deleted: items not in the sheet stay as they are.",
            $"• Up to {MaxRows} rows per file. You'll see a preview before anything is saved.",
        },
        new[]
        {
            new object[] { "Basmati Rice", "Kg", 25, 5, 95, "Grains", "Sharma Traders", "9876543210", "" },
            new object[] { "Paneer", "Kg", 4, 2, 380, "Dairy", "Amul Dairy", "", "Keep refrigerated" },
            new object[] { "Cooking Oil", "Liter", 15, 5, 140, "Oils", "", "", "" },
            new object[] { "Eggs", "Dozen", 10, 3, 72, "Dairy", "", "", "" },
        });

    public async Task<InventoryImportResult> ImportAsync(Stream xlsx, bool updateExisting, bool apply, string? userName)
    {
        var rows = ReadRows(xlsx, "Inventory", Columns, MaxRows, ParseRow);

        var items = await _db.InventoryItems.ToListAsync();
        var itemsByName = items.GroupBy(i => Key(i.Name)).ToDictionary(g => g.Key, g => g.ToList());

        var seenInFile = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) row.Errors.Add("Item name is required.");
            else if (row.Name.Length > 200) row.Errors.Add("Item name is longer than 200 characters.");

            if (row.CurrentQuantity < 0) row.Errors.Add("Current quantity cannot be negative.");
            if (row.MinimumQuantity < 0) row.Errors.Add("Minimum quantity cannot be negative.");
            if (row.CostPerUnit < 0) row.Errors.Add("Cost per unit cannot be negative.");
            if (row.CurrentQuantity > 99_999_999 || row.MinimumQuantity > 99_999_999 || row.CostPerUnit > 99_999_999)
                row.Errors.Add("A number is too large.");
            if (row.Category?.Length > 100) row.Errors.Add("Category is longer than 100 characters.");
            if (row.Supplier?.Length > 200) row.Errors.Add("Supplier is longer than 200 characters.");
            if (row.SupplierContact?.Length > 200) row.Errors.Add("Supplier contact is longer than 200 characters.");
            if (row.Description?.Length > 500) row.Errors.Add("Description is longer than 500 characters.");

            InventoryItem? existing = null;
            if (!string.IsNullOrWhiteSpace(row.Name))
            {
                var key = Key(row.Name);
                if (seenInFile.TryGetValue(key, out var firstRow))
                    row.Errors.Add($"Duplicate of row {firstRow} — each item name can appear only once.");
                else
                    seenInFile[key] = row.RowNumber;

                if (itemsByName.TryGetValue(key, out var matches))
                {
                    if (matches.Count > 1)
                        row.Errors.Add($"{matches.Count} items named \"{row.Name}\" already exist; rename them so the match is unambiguous.");
                    else
                        existing = matches[0];
                }
            }

            // Unit is required for new items; for existing ones a blank cell keeps the current unit
            if (existing == null && row.Unit == null && !row.Errors.Any(e => e.StartsWith("Unit")))
                row.Errors.Add("Unit is required.");

            if (row.Errors.Count == 0 && existing != null)
            {
                DescribeChanges(row, existing);
                row.Action = !updateExisting ? "Skip" : row.Changes.Count == 0 ? "Unchanged" : "Update";
            }

            if (row.Errors.Count > 0) row.Action = "Error";
        }

        var result = new InventoryImportResult { Rows = rows };
        result.Tally();

        if (!apply || result.CreateCount + result.UpdateCount == 0)
            return result;

        // ── Apply: one SaveChanges, so the import is all-or-nothing ──
        var tenantId = _tenantProvider.TenantId!;
        foreach (var row in rows.Where(r => r.Action is "Create" or "Update"))
        {
            if (row.Action == "Update")
            {
                var item = itemsByName[Key(row.Name)].Single();
                var oldQty = item.CurrentQuantity;

                if (row.Provided.Contains("Unit")) item.Unit = Enum.Parse<InventoryUnit>(row.Unit!);
                if (row.Provided.Contains("CurrentQuantity")) item.CurrentQuantity = row.CurrentQuantity!.Value;
                if (row.Provided.Contains("MinimumQuantity")) item.MinimumQuantity = row.MinimumQuantity!.Value;
                if (row.Provided.Contains("CostPerUnit")) item.CostPerUnit = row.CostPerUnit!.Value;
                if (row.Provided.Contains("Category")) item.Category = row.Category;
                if (row.Provided.Contains("Supplier")) item.Supplier = row.Supplier;
                if (row.Provided.Contains("SupplierContact")) item.SupplierContact = row.SupplierContact;
                if (row.Provided.Contains("Description")) item.Description = row.Description;
                item.UpdatedAt = DateTime.UtcNow;

                if (item.CurrentQuantity != oldQty)
                {
                    if (item.CurrentQuantity > oldQty) item.LastRestockedAt = DateTime.UtcNow;
                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        TenantId = tenantId,
                        InventoryItemId = item.Id,
                        QuantityChange = item.CurrentQuantity - oldQty,
                        QuantityAfter = item.CurrentQuantity,
                        ChangeType = "Adjustment",
                        Notes = "Excel import",
                        ChangedBy = userName
                    });
                }
            }
            else
            {
                var qty = row.CurrentQuantity ?? 0;
                var item = new InventoryItem
                {
                    TenantId = tenantId,
                    Name = row.Name.Trim(),
                    Unit = Enum.Parse<InventoryUnit>(row.Unit!),
                    CurrentQuantity = qty,
                    MinimumQuantity = row.MinimumQuantity ?? 0,
                    CostPerUnit = row.CostPerUnit ?? 0,
                    Category = row.Category,
                    Supplier = row.Supplier,
                    SupplierContact = row.SupplierContact,
                    Description = row.Description,
                    IsActive = true,
                    LastRestockedAt = qty > 0 ? DateTime.UtcNow : null
                };
                _db.InventoryItems.Add(item);

                // Same as creating an item by hand: log the opening stock
                if (qty > 0)
                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        TenantId = tenantId,
                        InventoryItemId = item.Id,
                        QuantityChange = qty,
                        QuantityAfter = qty,
                        ChangeType = "Restock",
                        Notes = "Initial stock (Excel import)",
                        ChangedBy = userName
                    });
            }
        }

        await _db.SaveChangesAsync();
        result.Applied = true;
        return result;
    }

    private static InventoryImportRow ParseRow(SheetRow s)
    {
        var r = new InventoryImportRow
        {
            RowNumber = s.RowNumber,
            Name = s.Text("Name"),
            Category = s.TextOrNull("Category"),
            Supplier = s.TextOrNull("Supplier"),
            SupplierContact = s.TextOrNull("SupplierContact"),
            Description = s.TextOrNull("Description")
        };
        foreach (var field in new[] { "Unit", "CurrentQuantity", "MinimumQuantity", "CostPerUnit", "Category", "Supplier", "SupplierContact", "Description" })
            if (s.Has(field)) r.Provided.Add(field);

        var unitText = s.Text("Unit");
        if (unitText.Length > 0)
        {
            if (UnitAliases.TryGetValue(Key(unitText), out var unit)) r.Unit = unit.ToString();
            else r.Errors.Add($"Unit \"{unitText}\" isn't recognized. Use one of: {string.Join(", ", Enum.GetNames<InventoryUnit>())}.");
        }

        r.CurrentQuantity = Round(s.Decimal("CurrentQuantity", "Current quantity", r.Errors));
        r.MinimumQuantity = Round(s.Decimal("MinimumQuantity", "Minimum quantity", r.Errors));
        r.CostPerUnit = Round(s.Decimal("CostPerUnit", "Cost per unit", r.Errors));
        return r;
    }

    private static decimal? Round(decimal? d) => d is null ? null : Math.Round(d.Value, 2);

    private static void DescribeChanges(InventoryImportRow row, InventoryItem item)
    {
        // Blank cells keep the current value — show that value in the preview, too
        if (!row.Provided.Contains("Unit")) row.Unit = item.Unit.ToString();
        if (!row.Provided.Contains("CurrentQuantity")) row.CurrentQuantity = item.CurrentQuantity;
        if (!row.Provided.Contains("MinimumQuantity")) row.MinimumQuantity = item.MinimumQuantity;
        if (!row.Provided.Contains("CostPerUnit")) row.CostPerUnit = item.CostPerUnit;
        if (!row.Provided.Contains("Category")) row.Category = item.Category;
        if (!row.Provided.Contains("Supplier")) row.Supplier = item.Supplier;
        if (!row.Provided.Contains("SupplierContact")) row.SupplierContact = item.SupplierContact;
        if (!row.Provided.Contains("Description")) row.Description = item.Description;

        if (row.Unit != item.Unit.ToString()) row.Changes.Add($"Unit {item.Unit} → {row.Unit}");
        if (row.CurrentQuantity != item.CurrentQuantity) row.Changes.Add($"Stock {item.CurrentQuantity:0.##} → {row.CurrentQuantity:0.##} {row.Unit}");
        if (row.MinimumQuantity != item.MinimumQuantity) row.Changes.Add($"Minimum {item.MinimumQuantity:0.##} → {row.MinimumQuantity:0.##}");
        if (row.CostPerUnit != item.CostPerUnit) row.Changes.Add($"Cost ₹{item.CostPerUnit:0.##} → ₹{row.CostPerUnit:0.##}");
        if (row.Category != item.Category) row.Changes.Add($"Category {item.Category ?? "—"} → {row.Category ?? "—"}");
        if (row.Supplier != item.Supplier) row.Changes.Add($"Supplier {item.Supplier ?? "—"} → {row.Supplier ?? "—"}");
        if (row.SupplierContact != item.SupplierContact) row.Changes.Add("Supplier contact updated");
        if (row.Description != item.Description) row.Changes.Add("Description updated");
    }

    private static Dictionary<string, InventoryUnit> BuildUnitAliases()
    {
        var map = new Dictionary<string, InventoryUnit>();
        void Add(InventoryUnit u, params string[] names) { foreach (var n in names) map[n] = u; }
        Add(InventoryUnit.Kg, "kg", "kgs", "kilo", "kilos", "kilogram", "kilograms");
        Add(InventoryUnit.Gram, "g", "gm", "gms", "gram", "grams", "gr");
        Add(InventoryUnit.Liter, "l", "lt", "ltr", "ltrs", "liter", "liters", "litre", "litres");
        Add(InventoryUnit.Ml, "ml", "mls", "millilitre", "milliliter", "millilitres", "milliliters");
        Add(InventoryUnit.Piece, "piece", "pieces", "pc", "pcs", "nos", "no", "number", "unit", "units", "each", "ea");
        Add(InventoryUnit.Dozen, "dozen", "dozens", "dz", "doz");
        Add(InventoryUnit.Box, "box", "boxes", "carton", "cartons");
        Add(InventoryUnit.Packet, "packet", "packets", "pkt", "pkts", "pack", "packs");
        Add(InventoryUnit.Bottle, "bottle", "bottles", "btl", "btls");
        Add(InventoryUnit.Can, "can", "cans", "tin", "tins");
        Add(InventoryUnit.Bunch, "bunch", "bunches");
        return map;
    }
}
