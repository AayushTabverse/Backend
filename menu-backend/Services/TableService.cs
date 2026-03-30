using menu_backend.Data;
using menu_backend.DTOs.Table;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace menu_backend.Services;

public class TableService : ITableService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly IHubContext<OrderHub> _hub;

    public TableService(AppDbContext db, ITenantProvider tenantProvider, IConfiguration config, IHubContext<OrderHub> hub)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _hub = hub;
    }

    public async Task<TableResponse> CreateTableAsync(CreateTableRequest request)
    {
        var tenantId = _tenantProvider.TenantId!;

        var exists = await _db.Tables.AnyAsync(t => t.TableNumber == request.TableNumber);
        if (exists)
            throw new InvalidOperationException($"Table '{request.TableNumber}' already exists.");

        var baseUrl = _config["App:BaseUrl"] ?? "https://yourapp.com";
        var table = new RestaurantTable
        {
            TenantId = tenantId,
            TableNumber = request.TableNumber,
            Label = request.Label,
            Capacity = request.Capacity,
            IsActive = true
        };

        // QR data format: baseUrl/menu/{tenantId}/{tableId}
        table.QrData = $"{baseUrl}/menu/{tenantId}/{table.Id}";

        _db.Tables.Add(table);
        await _db.SaveChangesAsync();

        return MapTable(table);
    }

    public async Task<List<TableResponse>> GetTablesAsync()
    {
        var activeStatuses = new[] 
        { 
            OrderStatus.Pending, OrderStatus.Accepted, 
            OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served 
        };

        var tables = await _db.Tables
            .OrderBy(t => t.TableNumber)
            .Select(t => new TableResponse
            {
                Id = t.Id,
                TableNumber = t.TableNumber,
                Label = t.Label,
                Capacity = t.Capacity,
                IsActive = t.IsActive,
                IsOccupied = t.Orders.Any(o => !o.IsDeleted && activeStatuses.Contains(o.Status)),
                ActiveOrderCount = t.Orders.Count(o => !o.IsDeleted && activeStatuses.Contains(o.Status)),
                IsCallingWaiter = t.IsCallingWaiter,
                WaiterCalledAt = t.WaiterCalledAt,
                QrCodeUrl = t.QrCodeUrl,
                QrData = t.QrData
            })
            .ToListAsync();

        // Load assignments
        var assignments = await _db.WaiterTableAssignments
            .Include(a => a.Waiter)
            .Where(a => !a.IsDeleted)
            .ToListAsync();

        foreach (var table in tables)
        {
            var tableAssignments = assignments.Where(a => a.TableId == table.Id).ToList();
            table.AssignedWaiterIds = tableAssignments.Select(a => a.WaiterId.ToString()).ToList();
            table.AssignedWaiterNames = tableAssignments.Select(a => a.Waiter?.FullName ?? "").ToList();
        }

        return tables;
    }

    public async Task<TableResponse?> GetTableAsync(Guid id)
    {
        var table = await _db.Tables.FindAsync(id);
        return table == null ? null : MapTable(table);
    }

    public async Task<TableResponse> UpdateTableAsync(Guid id, UpdateTableRequest request)
    {
        var table = await _db.Tables.FindAsync(id)
            ?? throw new KeyNotFoundException("Table not found.");

        if (request.TableNumber != null) table.TableNumber = request.TableNumber;
        if (request.Label != null) table.Label = request.Label;
        if (request.Capacity.HasValue) table.Capacity = request.Capacity.Value;
        if (request.IsActive.HasValue) table.IsActive = request.IsActive.Value;

        table.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapTable(table);
    }

    public async Task DeleteTableAsync(Guid id)
    {
        var table = await _db.Tables.FindAsync(id)
            ?? throw new KeyNotFoundException("Table not found.");

        // Soft-delete + rename to free the unique index slot for reuse
        table.IsDeleted = true;
        table.TableNumber = $"{table.TableNumber}_deleted_{table.Id}";
        table.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<byte[]> GenerateQrCodeAsync(Guid tableId, string baseUrl)
    {
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");

        var qrData = $"{baseUrl}/menu/{table.TenantId}/{table.Id}";
        table.QrData = qrData;

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrBytes = qrCode.GetGraphic(10);

        await _db.SaveChangesAsync();
        return qrBytes;
    }

    private static TableResponse MapTable(RestaurantTable table) => new()
    {
        Id = table.Id,
        TableNumber = table.TableNumber,
        Label = table.Label,
        Capacity = table.Capacity,
        IsActive = table.IsActive,
        IsCallingWaiter = table.IsCallingWaiter,
        WaiterCalledAt = table.WaiterCalledAt,
        QrCodeUrl = table.QrCodeUrl,
        QrData = table.QrData
    };

    public async Task<TableResponse> CallWaiterAsync(Guid tableId)
    {
        var table = await _db.Tables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tableId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Table not found.");

        table.IsCallingWaiter = true;
        table.WaiterCalledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(table.TenantId)
            .SendAsync("WaiterCalled", new { TableId = tableId, TableNumber = table.TableNumber });

        return MapTable(table);
    }

    public async Task DismissCallAsync(Guid tableId)
    {
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");

        table.IsCallingWaiter = false;
        table.WaiterCalledAt = null;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(table.TenantId)
            .SendAsync("WaiterCallDismissed", new { TableId = tableId, TableNumber = table.TableNumber });
    }

    public async Task AssignTablesToWaiterAsync(Guid waiterId, List<Guid> tableIds)
    {
        var tenantId = _tenantProvider.TenantId!;

        // Remove existing assignments for this waiter
        var existing = await _db.WaiterTableAssignments
            .Where(a => a.WaiterId == waiterId && !a.IsDeleted)
            .ToListAsync();

        foreach (var a in existing)
        {
            a.IsDeleted = true;
            a.UpdatedAt = DateTime.UtcNow;
        }

        // Add new assignments
        foreach (var tableId in tableIds)
        {
            _db.WaiterTableAssignments.Add(new WaiterTableAssignment
            {
                TenantId = tenantId,
                WaiterId = waiterId,
                TableId = tableId
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<WaiterAssignmentResponse> GetWaiterAssignmentAsync(Guid waiterId)
    {
        var waiter = await _db.Users.FindAsync(waiterId);
        var assignments = await _db.WaiterTableAssignments
            .Where(a => a.WaiterId == waiterId && !a.IsDeleted)
            .Select(a => a.TableId)
            .ToListAsync();

        return new WaiterAssignmentResponse
        {
            WaiterId = waiterId,
            WaiterName = waiter?.FullName ?? "",
            AssignedTableIds = assignments
        };
    }

    public async Task<List<WaiterAssignmentResponse>> GetAllAssignmentsAsync()
    {
        var assignments = await _db.WaiterTableAssignments
            .Include(a => a.Waiter)
            .Where(a => !a.IsDeleted)
            .ToListAsync();

        return assignments
            .GroupBy(a => a.WaiterId)
            .Select(g => new WaiterAssignmentResponse
            {
                WaiterId = g.Key,
                WaiterName = g.First().Waiter?.FullName ?? "",
                AssignedTableIds = g.Select(a => a.TableId).ToList()
            })
            .ToList();
    }
}
