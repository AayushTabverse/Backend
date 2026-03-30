using menu_backend.Data;
using menu_backend.DTOs.Inventory;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<InventoryItemResponse>> GetItemsAsync(string? category = null, bool? lowStockOnly = null)
    {
        var query = _db.InventoryItems.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(i => i.Category == category);

        if (lowStockOnly == true)
            query = query.Where(i => i.CurrentQuantity <= i.MinimumQuantity && i.MinimumQuantity > 0);

        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .Select(i => MapItem(i))
            .ToListAsync();
    }

    public async Task<InventoryItemResponse?> GetItemAsync(Guid id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        return item == null ? null : MapItem(item);
    }

    public async Task<InventoryItemResponse> CreateItemAsync(CreateInventoryItemRequest request)
    {
        if (!Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            throw new ArgumentException($"Invalid unit: {request.Unit}");

        var item = new InventoryItem
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            CurrentQuantity = request.CurrentQuantity,
            Unit = unit,
            MinimumQuantity = request.MinimumQuantity,
            CostPerUnit = request.CostPerUnit,
            Supplier = request.Supplier,
            SupplierContact = request.SupplierContact,
            IsActive = true,
            LastRestockedAt = request.CurrentQuantity > 0 ? DateTime.UtcNow : null
        };

        _db.InventoryItems.Add(item);

        // Log initial stock
        if (request.CurrentQuantity > 0)
        {
            _db.InventoryLogs.Add(new InventoryLog
            {
                TenantId = item.TenantId,
                InventoryItemId = item.Id,
                QuantityChange = request.CurrentQuantity,
                QuantityAfter = request.CurrentQuantity,
                ChangeType = "Restock",
                Notes = "Initial stock",
                ChangedBy = "System"
            });
        }

        await _db.SaveChangesAsync();
        return MapItem(item);
    }

    public async Task<InventoryItemResponse> UpdateItemAsync(Guid id, UpdateInventoryItemRequest request)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        if (request.Name != null) item.Name = request.Name;
        if (request.Description != null) item.Description = request.Description;
        if (request.Category != null) item.Category = request.Category;
        if (request.Unit != null && Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            item.Unit = unit;
        if (request.MinimumQuantity.HasValue) item.MinimumQuantity = request.MinimumQuantity.Value;
        if (request.CostPerUnit.HasValue) item.CostPerUnit = request.CostPerUnit.Value;
        if (request.Supplier != null) item.Supplier = request.Supplier;
        if (request.SupplierContact != null) item.SupplierContact = request.SupplierContact;
        if (request.IsActive.HasValue) item.IsActive = request.IsActive.Value;

        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapItem(item);
    }

    public async Task DeleteItemAsync(Guid id)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<InventoryItemResponse> AdjustQuantityAsync(Guid id, AdjustQuantityRequest request, string? userName)
    {
        var item = await _db.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException("Inventory item not found.");

        var validTypes = new[] { "Restock", "Usage", "Wastage", "Adjustment" };
        if (!validTypes.Contains(request.ChangeType))
            throw new ArgumentException($"Invalid change type: {request.ChangeType}");

        decimal change;
        if (request.ChangeType == "Restock" || request.ChangeType == "Adjustment")
        {
            change = Math.Abs(request.Quantity);
        }
        else
        {
            change = -Math.Abs(request.Quantity);
        }

        item.CurrentQuantity += change;
        if (item.CurrentQuantity < 0) item.CurrentQuantity = 0;

        if (request.ChangeType == "Restock")
            item.LastRestockedAt = DateTime.UtcNow;

        item.UpdatedAt = DateTime.UtcNow;

        _db.InventoryLogs.Add(new InventoryLog
        {
            TenantId = item.TenantId,
            InventoryItemId = item.Id,
            QuantityChange = change,
            QuantityAfter = item.CurrentQuantity,
            ChangeType = request.ChangeType,
            Notes = request.Notes,
            ChangedBy = userName
        });

        await _db.SaveChangesAsync();
        return MapItem(item);
    }

    public async Task<List<InventoryLogResponse>> GetLogsAsync(Guid? itemId = null, int days = 30)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        var query = _db.InventoryLogs
            .Include(l => l.InventoryItem)
            .Where(l => l.CreatedAt >= fromDate);

        if (itemId.HasValue)
            query = query.Where(l => l.InventoryItemId == itemId.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .Select(l => new InventoryLogResponse
            {
                Id = l.Id,
                InventoryItemId = l.InventoryItemId,
                ItemName = l.InventoryItem != null ? l.InventoryItem.Name : "",
                QuantityChange = l.QuantityChange,
                QuantityAfter = l.QuantityAfter,
                ChangeType = l.ChangeType,
                Notes = l.Notes,
                ChangedBy = l.ChangedBy,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync()
    {
        var items = await _db.InventoryItems.Where(i => i.IsActive).ToListAsync();

        var lowStock = items.Where(i => i.MinimumQuantity > 0 && i.CurrentQuantity <= i.MinimumQuantity).ToList();
        var outOfStock = items.Where(i => i.CurrentQuantity <= 0).ToList();

        var categoryBreakdown = items
            .GroupBy(i => i.Category ?? "Uncategorized")
            .Select(g => new CategorySummary
            {
                Category = g.Key,
                ItemCount = g.Count(),
                TotalValue = g.Sum(i => i.CurrentQuantity * i.CostPerUnit)
            })
            .OrderByDescending(c => c.TotalValue)
            .ToList();

        return new InventorySummaryResponse
        {
            TotalItems = items.Count,
            LowStockItems = lowStock.Count,
            OutOfStockItems = outOfStock.Count,
            TotalInventoryValue = items.Sum(i => i.CurrentQuantity * i.CostPerUnit),
            LowStockAlerts = lowStock.Select(MapItem).ToList(),
            CategoryBreakdown = categoryBreakdown
        };
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _db.InventoryItems
            .Where(i => i.Category != null && i.Category != "")
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    private static InventoryItemResponse MapItem(InventoryItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Category = item.Category,
        CurrentQuantity = item.CurrentQuantity,
        Unit = item.Unit.ToString(),
        MinimumQuantity = item.MinimumQuantity,
        CostPerUnit = item.CostPerUnit,
        Supplier = item.Supplier,
        SupplierContact = item.SupplierContact,
        LastRestockedAt = item.LastRestockedAt,
        IsActive = item.IsActive,
        IsLowStock = item.MinimumQuantity > 0 && item.CurrentQuantity <= item.MinimumQuantity,
        TotalValue = item.CurrentQuantity * item.CostPerUnit,
        CreatedAt = item.CreatedAt
    };

    public async Task DeductForOrderAsync(Guid orderId, string? changedBy = null)
    {
        // Load order items
        var orderItems = await _db.OrderItems
            .Where(oi => oi.OrderId == orderId)
            .ToListAsync();

        if (orderItems.Count == 0) return;

        // Load all ingredient mappings for the menu items in this order
        var menuItemIds = orderItems.Select(oi => oi.MenuItemId).Distinct().ToArray();
        var ingredients = new List<MenuItemIngredient>();
        foreach (var menuItemId in menuItemIds)
        {
            var itemIngredients = await _db.MenuItemIngredients
                .Where(i => i.MenuItemId == menuItemId)
                .Include(i => i.InventoryItem)
                .ToListAsync();
            ingredients.AddRange(itemIngredients);
        }

        if (ingredients.Count == 0) return;

        // Aggregate deductions per inventory item
        var deductions = new Dictionary<Guid, decimal>();
        foreach (var oi in orderItems)
        {
            var itemIngredients = ingredients.Where(i => i.MenuItemId == oi.MenuItemId);
            foreach (var ing in itemIngredients)
            {
                var totalUsage = ing.QuantityUsed * oi.Quantity;
                if (deductions.ContainsKey(ing.InventoryItemId))
                    deductions[ing.InventoryItemId] += totalUsage;
                else
                    deductions[ing.InventoryItemId] = totalUsage;
            }
        }

        // Apply deductions and create logs
        foreach (var (inventoryItemId, totalDeduction) in deductions)
        {
            var invItem = await _db.InventoryItems.FindAsync(inventoryItemId);
            if (invItem == null) continue;

            invItem.CurrentQuantity = Math.Max(0, invItem.CurrentQuantity - totalDeduction);
            invItem.UpdatedAt = DateTime.UtcNow;

            _db.InventoryLogs.Add(new Models.InventoryLog
            {
                InventoryItemId = inventoryItemId,
                QuantityChange = -totalDeduction,
                QuantityAfter = invItem.CurrentQuantity,
                ChangeType = "Usage",
                Notes = $"Auto-deducted for order",
                ChangedBy = changedBy ?? "System"
            });
        }

        await _db.SaveChangesAsync();
    }
}
