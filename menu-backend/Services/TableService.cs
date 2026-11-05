using menu_backend.Helpers;
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
    private const int ReservationHoldMinutes = ReservationRules.HoldMinutes;

    private static readonly OrderStatus[] ActiveOrderStatuses =
    {
        OrderStatus.Pending, OrderStatus.Accepted,
        OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served
    };

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
                BillRequestedAt = t.BillRequestedAt,
                BillRequestedBy = t.BillRequestedBy,
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

        // Tables held by seated waitlist / reservation parties
        var held = await _db.WaitlistEntryTables
            .Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated)
            .Select(x => new
            {
                x.TableId,
                x.IsPrimary,
                x.WaitlistEntryId,
                x.WaitlistEntry!.CustomerName,
                x.WaitlistEntry.CustomerMobile,
                x.WaitlistEntry.PartySize
            })
            .ToListAsync();

        var mergedInto = await _db.Tables
            .Where(t => t.MergedIntoTableId != null)
            .Select(t => new { t.Id, MainId = t.MergedIntoTableId!.Value })
            .ToListAsync();

        // Confirmed bookings starting within the hour, or late but still within the grace period
        var now = DateTime.UtcNow;
        var graceMinutes = await _db.Tenants
            .Where(t => t.TenantId == _tenantProvider.TenantId)
            .Select(t => t.ReservationGraceMinutes)
            .FirstOrDefaultAsync();
        var upcoming = await _db.ReservationTables
            .Where(x => x.Reservation!.Status == ReservationStatus.Confirmed
                     && x.Reservation.ReservedFor <= now.AddMinutes(ReservationHoldMinutes)
                     && x.Reservation.ReservedFor >= now.AddMinutes(-Math.Max(graceMinutes, 30)))
            .Select(x => new
            {
                x.TableId,
                x.ReservationId,
                x.Reservation!.CustomerName,
                x.Reservation.PartySize,
                x.Reservation.ReservedFor
            })
            .ToListAsync();

        var byId = tables.ToDictionary(t => t.Id);
        foreach (var table in tables)
        {
            table.CombinedCapacity = table.Capacity;

            var merge = mergedInto.FirstOrDefault(m => m.Id == table.Id);
            if (merge != null && byId.TryGetValue(merge.MainId, out var main))
            {
                table.MergedIntoTableId = main.Id;
                table.MergedIntoTableNumber = main.TableNumber;
            }

            var children = mergedInto.Where(m => m.MainId == table.Id && byId.ContainsKey(m.Id)).ToList();
            if (children.Count > 0)
            {
                table.MergedTableNumbers = children.Select(c => byId[c.Id].TableNumber).ToList();
                table.CombinedCapacity += children.Sum(c => byId[c.Id].Capacity);
            }

            var booking = upcoming.Where(u => u.TableId == table.Id).OrderBy(u => u.ReservedFor).FirstOrDefault();
            if (booking != null)
            {
                table.ReservationId = booking.ReservationId;
                table.ReservationCustomerName = booking.CustomerName;
                table.ReservationPartySize = booking.PartySize;
                table.ReservationTime = booking.ReservedFor;
            }

            var hold = held.FirstOrDefault(h => h.TableId == table.Id);
            if (hold == null) continue;

            table.WaitlistEntryId = hold.WaitlistEntryId;
            table.WaitlistCustomerName = hold.CustomerName;
            table.WaitlistCustomerMobile = hold.CustomerMobile;
            table.WaitlistPartySize = hold.PartySize;
            table.IsPrimaryWaitlistTable = hold.IsPrimary;
            table.MergedWithTableNumbers = held
                .Where(h => h.WaitlistEntryId == hold.WaitlistEntryId && h.TableId != table.Id)
                .Select(h => byId.TryGetValue(h.TableId, out var t) ? t.TableNumber : "")
                .ToList();
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

    // ── Merge & transfer ──

    public async Task<TableResponse> MergeTablesAsync(Guid mainTableId, List<Guid> tableIds)
    {
        var ids = tableIds.Distinct().Where(id => id != mainTableId).ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one table to merge.");

        // A restaurant has few tables; load them all (the MySQL provider can't translate Guid-list Contains)
        var all = await _db.Tables.ToListAsync();
        var main = all.FirstOrDefault(t => t.Id == mainTableId)
            ?? throw new KeyNotFoundException("Table not found.");
        if (!main.IsActive)
            throw new InvalidOperationException($"Table {main.TableNumber} is inactive.");
        if (main.MergedIntoTableId != null)
        {
            var mainOf = all.FirstOrDefault(t => t.Id == main.MergedIntoTableId);
            throw new InvalidOperationException(
                $"Table {main.TableNumber} is already merged into {mainOf?.TableNumber}. Merge into {mainOf?.TableNumber} instead.");
        }

        var toMerge = all.Where(t => ids.Contains(t.Id)).ToList();
        if (toMerge.Count != ids.Count)
            throw new KeyNotFoundException("One or more tables were not found.");

        var heldBy = await HeldTablesAsync();
        heldBy.TryGetValue(main.Id, out var mainParty);

        foreach (var t in toMerge)
        {
            if (!t.IsActive)
                throw new InvalidOperationException($"Table {t.TableNumber} is inactive.");
            if (t.MergedIntoTableId != null && t.MergedIntoTableId != main.Id)
                throw new InvalidOperationException($"Table {t.TableNumber} is merged with another table. Un-merge it first.");
            if (all.Any(c => c.MergedIntoTableId == t.Id))
                throw new InvalidOperationException($"Table {t.TableNumber} has its own merged tables. Un-merge them first.");
            if (heldBy.TryGetValue(t.Id, out var party) && party != mainParty)
                throw new InvalidOperationException($"Table {t.TableNumber} is held for another party.");
        }

        // Running orders on the merged tables move to the main table's bill
        var mergeIds = toMerge.Select(t => t.Id).ToList();
        var orders = (await _db.Orders.Where(o => ActiveOrderStatuses.Contains(o.Status)).ToListAsync())
            .Where(o => o.TableId != null && mergeIds.Contains(o.TableId.Value))
            .ToList();
        foreach (var order in orders)
            order.TableId = main.Id;

        foreach (var t in toMerge)
        {
            t.MergedIntoTableId = main.Id;
            t.IsCallingWaiter = false;
            t.WaiterCalledAt = null;
            t.BillRequestedAt = null;
            t.BillRequestedBy = null;
        }

        await _db.SaveChangesAsync();
        await BroadcastTablesChangedAsync(main.TenantId);

        return (await GetTablesAsync()).First(t => t.Id == main.Id);
    }

    public async Task UnmergeAsync(Guid tableId)
    {
        var all = await _db.Tables.ToListAsync();
        var table = all.FirstOrDefault(t => t.Id == tableId)
            ?? throw new KeyNotFoundException("Table not found.");

        var detach = table.MergedIntoTableId != null
            ? new List<RestaurantTable> { table }
            : all.Where(t => t.MergedIntoTableId == table.Id).ToList();
        if (detach.Count == 0)
            throw new InvalidOperationException($"Table {table.TableNumber} isn't merged.");

        foreach (var t in detach)
            t.MergedIntoTableId = null;

        // A detached table no longer belongs to the seated party's hold
        var detachIds = detach.Select(t => t.Id).ToList();
        var links = (await _db.WaitlistEntryTables
                .Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated && !x.IsPrimary)
                .ToListAsync())
            .Where(x => detachIds.Contains(x.TableId));
        foreach (var link in links)
            link.IsDeleted = true;

        await _db.SaveChangesAsync();
        await BroadcastTablesChangedAsync(table.TenantId);
    }

    public async Task<TableResponse> TransferAsync(Guid fromTableId, Guid toTableId)
    {
        if (fromTableId == toTableId)
            throw new InvalidOperationException("Pick a different table.");

        var all = await _db.Tables.ToListAsync();
        var from = all.FirstOrDefault(t => t.Id == fromTableId)
            ?? throw new KeyNotFoundException("Table not found.");
        var to = all.FirstOrDefault(t => t.Id == toTableId)
            ?? throw new KeyNotFoundException("Table not found.");

        if (from.MergedIntoTableId != null)
        {
            var mainOf = all.FirstOrDefault(t => t.Id == from.MergedIntoTableId);
            throw new InvalidOperationException(
                $"Table {from.TableNumber} is merged into {mainOf?.TableNumber}. Transfer {mainOf?.TableNumber} instead.");
        }

        if (!to.IsActive)
            throw new InvalidOperationException($"Table {to.TableNumber} is inactive.");
        if (to.MergedIntoTableId != null || all.Any(t => t.MergedIntoTableId == to.Id))
            throw new InvalidOperationException($"Table {to.TableNumber} is part of a merge. Pick a free table.");

        var activeOrders = await _db.Orders.Where(o => ActiveOrderStatuses.Contains(o.Status)).ToListAsync();
        if (activeOrders.Any(o => o.TableId == to.Id))
            throw new InvalidOperationException($"Table {to.TableNumber} is occupied. Use merge to combine two running tables.");

        var heldBy = await HeldTablesAsync();
        if (heldBy.ContainsKey(to.Id))
            throw new InvalidOperationException($"Table {to.TableNumber} is held for another party.");

        var ordersToMove = activeOrders.Where(o => o.TableId == from.Id).ToList();
        var holdLinks = (await _db.WaitlistEntryTables
                .Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated)
                .ToListAsync())
            .Where(x => x.TableId == from.Id)
            .ToList();
        var children = all.Where(t => t.MergedIntoTableId == from.Id).ToList();

        if (ordersToMove.Count == 0 && holdLinks.Count == 0 && children.Count == 0)
            throw new InvalidOperationException($"Table {from.TableNumber} has nothing to transfer.");

        foreach (var order in ordersToMove) order.TableId = to.Id;
        foreach (var link in holdLinks) link.TableId = to.Id;
        foreach (var child in children) child.MergedIntoTableId = to.Id;

        if (from.IsCallingWaiter)
        {
            to.IsCallingWaiter = true;
            to.WaiterCalledAt = from.WaiterCalledAt;
            from.IsCallingWaiter = false;
            from.WaiterCalledAt = null;
        }
        if (from.BillRequestedAt != null)
        {
            to.BillRequestedAt = from.BillRequestedAt;
            to.BillRequestedBy = from.BillRequestedBy;
            from.BillRequestedAt = null;
            from.BillRequestedBy = null;
        }

        await _db.SaveChangesAsync();
        await BroadcastTablesChangedAsync(from.TenantId);

        return (await GetTablesAsync()).First(t => t.Id == to.Id);
    }

    /// <summary>Table id → waitlist entry holding it (seated parties).</summary>
    private async Task<Dictionary<Guid, Guid>> HeldTablesAsync() =>
        (await _db.WaitlistEntryTables
            .Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated)
            .Select(x => new { x.TableId, x.WaitlistEntryId })
            .ToListAsync())
        .GroupBy(x => x.TableId)
        .ToDictionary(g => g.Key, g => g.First().WaitlistEntryId);

    /// <summary>Waiter/admin screens and the kitchen (table numbers on tickets) refresh on this.</summary>
    private Task BroadcastTablesChangedAsync(string tenantId) =>
        _hub.BroadcastTablesChangedAsync(tenantId);

    private static TableResponse MapTable(RestaurantTable table) => new()
    {
        Id = table.Id,
        TableNumber = table.TableNumber,
        Label = table.Label,
        Capacity = table.Capacity,
        IsActive = table.IsActive,
        IsCallingWaiter = table.IsCallingWaiter,
        WaiterCalledAt = table.WaiterCalledAt,
        BillRequestedAt = table.BillRequestedAt,
        BillRequestedBy = table.BillRequestedBy,
        QrCodeUrl = table.QrCodeUrl,
        QrData = table.QrData
    };

    public async Task<TableResponse> MoveOrdersAsync(Guid fromTableId, Guid toTableId, bool separateParty)
    {
        if (fromTableId == toTableId)
            throw new InvalidOperationException("Pick a different table.");

        var all = await _db.Tables.ToListAsync();
        var from = all.FirstOrDefault(t => t.Id == fromTableId)
            ?? throw new KeyNotFoundException("Table not found.");
        var to = all.FirstOrDefault(t => t.Id == toTableId)
            ?? throw new KeyNotFoundException("Table not found.");
        // Dropped on a table that's merged into another: the bill lives on the main table
        if (to.MergedIntoTableId is Guid mainId)
            to = all.FirstOrDefault(t => t.Id == mainId) ?? to;
        if (from.Id == to.Id)
            throw new InvalidOperationException("These tables are already on one bill.");
        if (from.MergedIntoTableId != null)
            throw new InvalidOperationException($"Table {from.TableNumber} is merged with another table. Move the main table instead.");
        if (!to.IsActive)
            throw new InvalidOperationException($"Table {to.TableNumber} is inactive.");

        var active = await _db.Orders.Where(o => ActiveOrderStatuses.Contains(o.Status)).ToListAsync();
        var moving = active.Where(o => o.TableId == from.Id).ToList();
        if (moving.Count == 0)
            throw new InvalidOperationException($"Table {from.TableNumber} has no running orders to move.");
        var staying = active.Where(o => o.TableId == to.Id).ToList();

        if (separateParty)
        {
            // Keep each of the moved parties apart from the target table's parties. Phones keep their own
            // session; staff-entered orders (no session) or a clash with a target party get a new party id.
            var targetParties = staying.Select(o => o.CustomerSessionId).ToHashSet();
            var newIds = new Dictionary<string, string>();
            foreach (var o in moving)
            {
                var key = o.CustomerSessionId ?? "";
                if (o.CustomerSessionId != null && !targetParties.Contains(o.CustomerSessionId)) continue;
                if (!newIds.TryGetValue(key, out var id))
                    newIds[key] = id = $"w-{Guid.NewGuid():N}"[..20];
                o.CustomerSessionId = id;
            }
        }
        else
        {
            // One bill with the target table: join its (first) party
            var targetParty = staying.OrderBy(o => o.CreatedAt).Select(o => o.CustomerSessionId).FirstOrDefault();
            foreach (var o in moving) o.CustomerSessionId = targetParty;
        }

        foreach (var o in moving) o.TableId = to.Id;

        // The seated waitlist party moves with its orders; the old table is free again
        var holdLinks = (await _db.WaitlistEntryTables
                .Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated)
                .ToListAsync())
            .Where(x => x.TableId == from.Id)
            .ToList();
        foreach (var link in holdLinks) link.TableId = to.Id;

        if (from.BillRequestedAt != null && to.BillRequestedAt == null)
        {
            to.BillRequestedAt = from.BillRequestedAt;
            to.BillRequestedBy = from.BillRequestedBy;
        }
        from.BillRequestedAt = null;
        from.BillRequestedBy = null;
        from.IsCallingWaiter = false;
        from.WaiterCalledAt = null;

        await _db.SaveChangesAsync();
        await BroadcastTablesChangedAsync(from.TenantId);

        return (await GetTablesAsync()).First(t => t.Id == to.Id);
    }

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

    public async Task<TableResponse> RequestBillAsync(Guid tableId, string? requestedBy)
    {
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");
        // A merged table's bill is the main table's
        if (table.MergedIntoTableId is Guid mainId)
            table = await _db.Tables.FindAsync(mainId) ?? table;

        var hasOrders = await _db.Orders.AnyAsync(o => o.TableId == table.Id
            && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        if (!hasOrders)
            throw new InvalidOperationException($"Table {table.TableNumber} has no open orders to bill.");

        table.BillRequestedAt = DateTime.UtcNow;
        table.BillRequestedBy = requestedBy;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(table.TenantId)
            .SendAsync("BillRequested", new { TableId = table.Id, TableNumber = table.TableNumber, RequestedBy = requestedBy });
        await BroadcastTablesChangedAsync(table.TenantId);
        return MapTable(table);
    }

    public async Task CancelBillRequestAsync(Guid tableId)
    {
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");
        if (table.MergedIntoTableId is Guid mainId)
            table = await _db.Tables.FindAsync(mainId) ?? table;

        table.BillRequestedAt = null;
        table.BillRequestedBy = null;
        await _db.SaveChangesAsync();
        await BroadcastTablesChangedAsync(table.TenantId);
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
