using Microsoft.Extensions.Caching.Memory;
using menu_backend.Data;
using menu_backend.DTOs.Order;
using menu_backend.DTOs.Loyalty;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using menu_backend.Helpers;

namespace menu_backend.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IPrintService _printService;
    private readonly IInventoryService _inventoryService;
    private readonly IWaitlistService _waitlistService;
    private readonly OrderingAvailability _ordering;
    private readonly LoyaltyService _loyalty;
    private readonly AccountingService _accounting;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    /// <summary>Wrong room PINs allowed before that room's QR ordering pauses (stops guessing the 4 digits).</summary>
    private const int MaxPinTries = 5;
    private static readonly TimeSpan PinLockout = TimeSpan.FromMinutes(15);

    public OrderService(
        AppDbContext db,
        ITenantProvider tenantProvider,
        IHubContext<OrderHub> hub,
        IPrintService printService,
        IInventoryService inventoryService,
        IWaitlistService waitlistService,
        OrderingAvailability ordering,
        LoyaltyService loyalty,
        AccountingService accounting,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _cache = cache;
        _ordering = ordering;
        _loyalty = loyalty;
        _accounting = accounting;
        _db = db;
        _tenantProvider = tenantProvider;
        _hub = hub;
        _printService = printService;
        _inventoryService = inventoryService;
        _waitlistService = waitlistService;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string tenantId, string? customerSessionId = null, bool placedByStaff = false)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Restaurant not found.");
        var isTakeaway = string.Equals(request.Type, nameof(OrderType.Takeaway), StringComparison.OrdinalIgnoreCase);
        var isRoom = string.Equals(request.Type, nameof(OrderType.RoomService), StringComparison.OrdinalIgnoreCase);

        // Opening hours and the distance check apply to customers' QR orders, not staff. Room service
        // is secured by check-in and the room PIN instead (and often runs round the clock).
        if (!placedByStaff && !isRoom)
            await _ordering.EnsureCustomerCanOrderAsync(tenant, request.Latitude, request.Longitude, request.LocationAccuracy);

        RestaurantTable? table = null;
        Room? room = null;
        Stay? stay = null;
        if (isRoom)
        {
            if (!tenant.HotelModeEnabled)
                throw new InvalidOperationException("Room service isn't available.");
            room = await _db.Rooms.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
                ?? throw new KeyNotFoundException("Room not found.");
            stay = await ActiveStayAsync(room.Id)
                ?? throw new InvalidOperationException($"Room {room.RoomNumber} isn't checked in. Please call reception.");
            if (!placedByStaff && !string.IsNullOrEmpty(stay.Pin))
            {
                var key = $"room-pin-fails:{room.Id}";
                var fails = _cache.TryGetValue(key, out int n) ? n : 0;
                if (fails >= MaxPinTries)
                    throw new InvalidOperationException("Too many wrong PINs. Please call reception.");
                if (stay.Pin != request.RoomPin?.Trim())
                {
                    _cache.Set(key, fails + 1, PinLockout);
                    throw new InvalidOperationException("That room PIN isn't right. It's on your check-in slip, or ask reception.");
                }
                _cache.Remove(key);
            }
        }
        else if (isTakeaway)
        {
            if (!placedByStaff && !tenant.TakeawayEnabled)
                throw new InvalidOperationException("This restaurant isn't taking takeaway orders by QR right now. Please order at the counter.");
            if (!placedByStaff && (string.IsNullOrWhiteSpace(request.CustomerName) || NormalizeMobile(request.CustomerMobile)?.Length != 10))
                throw new InvalidOperationException("Please enter your name and 10-digit mobile number so we can call you when it's ready.");
        }
        else
        {
            if (request.TableId == null)
                throw new InvalidOperationException("Choose a table for this order.");

            table = await _db.Tables
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == request.TableId && t.TenantId == tenantId && !t.IsDeleted)
                ?? throw new KeyNotFoundException("Table not found.");

            // Merged tables share the main table's bill: only the main table takes orders
            if (table.MergedIntoTableId != null)
            {
                var mainNumber = await _db.Tables
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == table.MergedIntoTableId && t.TenantId == tenantId && !t.IsDeleted)
                    .Select(t => t.TableNumber)
                    .FirstOrDefaultAsync();
                if (mainNumber != null)
                    throw new InvalidOperationException(
                        $"Table {table.TableNumber} is joined with Table {mainNumber}. Please place your order from Table {mainNumber}.");
            }
        }

        // Generate order number: ORD-YYYYMMDD-XXXX (restaurant-local day)
        var localToday = BusinessClock.Today;
        var today = localToday.ToString("yyyyMMdd");
        var (dayStart, dayEnd) = BusinessClock.DayRangeUtc(localToday);
        var countToday = await _db.Orders
            .IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == tenantId && o.CreatedAt >= dayStart && o.CreatedAt < dayEnd);
        var orderNumber = $"ORD-{today}-{(countToday + 1):D4}";

        var order = new Order
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            TableId = table?.Id,
            Type = isRoom ? OrderType.RoomService : isTakeaway ? OrderType.Takeaway : OrderType.DineIn,
            RoomId = room?.Id,
            StayId = stay?.Id,
            ChargeToRoom = isRoom && request.ChargeToRoom,
            Status = OrderStatus.Pending,
            SpecialInstructions = request.SpecialInstructions,
            CustomerSessionId = customerSessionId,
            CustomerName = stay?.GuestName ?? (string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim()),
            CustomerMobile = stay?.GuestMobile ?? NormalizeMobile(request.CustomerMobile),
            TokenNumber = room?.RoomNumber,
            PackingFee = isTakeaway ? tenant.TakeawayPackingFee : 0
        };

        if (isTakeaway)
        {
            // Pickup token, restarting daily: T-001, T-002…
            var takeawaysToday = await _db.Orders
                .IgnoreQueryFilters()
                .CountAsync(o => o.TenantId == tenantId && o.Type == OrderType.Takeaway && o.CreatedAt >= dayStart && o.CreatedAt < dayEnd);
            order.TokenNumber = $"T-{(takeawaysToday + 1):D3}";
        }

        int maxPrepTime = 0;

        foreach (var itemReq in request.Items)
        {
            var menuItem = await _db.MenuItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == itemReq.MenuItemId && m.TenantId == tenantId && !m.IsDeleted)
                ?? throw new KeyNotFoundException($"Menu item {itemReq.MenuItemId} not found.");

            if (!menuItem.IsAvailable)
                throw new InvalidOperationException($"'{menuItem.Name}' is currently unavailable.");

            var isParcel = isTakeaway || itemReq.IsParcel;
            maxPrepTime = Math.Max(maxPrepTime, menuItem.PreparationTimeMinutes);

            order.Items.Add(new OrderItem
            {
                TenantId = tenantId,
                MenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                Quantity = itemReq.Quantity,
                UnitPrice = menuItem.Price,
                TotalPrice = menuItem.Price * itemReq.Quantity,
                IsParcel = isParcel,
                PackagingCharge = isParcel ? menuItem.PackagingCharge : 0,
                Modifiers = itemReq.Modifiers,
                Notes = itemReq.Notes
            });
        }

        ApplyTotals(order, TaxRate(tenant), subtractDiscount: false);
        order.EstimatedMinutes = maxPrepTime + 5; // buffer

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _printService.QueueKotAsync(order.Id, tenantId, "OnOrder");

        // Notify kitchen via SignalR
        var response = MapOrder(order, table?.TableNumber ?? "");
        await _hub.Clients.Group(tenantId).SendAsync("NewOrder", response);
        // The table may have just become occupied
        if (table != null)
            await _hub.Clients.Group($"waitlist-queue-{tenantId}").SendAsync("TableBoardChanged");

        return response;
    }

    private Task<Stay?> ActiveStayAsync(Guid roomId) =>
        _db.Stays.IgnoreQueryFilters()
            .Where(s => s.RoomId == roomId && !s.IsDeleted && s.Status == StayStatus.Active)
            .OrderByDescending(s => s.CheckInAt)
            .FirstOrDefaultAsync();

    private static decimal TaxRate(Tenant? tenant) =>
        tenant != null ? (tenant.CgstPercent + tenant.SgstPercent + tenant.ServiceChargePercent) / 100m : 0.05m;

    /// <summary>
    /// Food subtotal, packing (parcel items + flat takeaway fee) and tax on both — packing is part
    /// of the taxable sale. <paramref name="subtractDiscount"/> takes DiscountAmount off the total.
    /// </summary>
    private static void ApplyTotals(Order order, decimal taxRate, bool subtractDiscount)
    {
        order.SubTotal = order.Items.Sum(i => i.TotalPrice);
        order.PackagingCharge = order.Items.Count == 0 ? 0
            : order.Items.Sum(i => i.PackagingCharge * i.Quantity) + order.PackingFee;
        // Loyalty points come off before tax; never more than what's left on the order
        order.LoyaltyDiscount = Math.Min(order.LoyaltyDiscount, order.SubTotal + order.PackagingCharge);
        var taxable = order.SubTotal + order.PackagingCharge - order.LoyaltyDiscount;
        order.Tax = Math.Round(taxable * taxRate, 2);
        order.TotalAmount = taxable + order.Tax - (subtractDiscount ? order.DiscountAmount : 0);
    }

    private static string? NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    /// <summary>
    /// Staff screens get every order change on the tenant group; the customer gets their own
    /// order's changes on its order group (order tracking and the table's menu page follow it).
    /// </summary>
    private async Task BroadcastOrderUpdatedAsync(OrderResponse response, string tenantId)
    {
        await _hub.Clients.Group(tenantId).SendAsync("OrderStatusUpdated", response);
        await _hub.Clients.Group($"order-{response.Id}").SendAsync("OrderStatusUpdated", response);
    }

    public async Task<OrderResponse?> GetOrderAsync(Guid id)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return null;
        return MapOrder(order, order.Table?.TableNumber ?? "");
    }

    public async Task<OrderResponse> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Order not found.");

        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            throw new ArgumentException("Invalid order status.");

        order.Status = status;
        switch (status)
        {
            case OrderStatus.Accepted:
                order.AcceptedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Ready:
                order.PreparedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Served:
                order.ServedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Completed:
                order.CompletedAt = DateTime.UtcNow;
                break;
        }

        if (request.EstimatedMinutes.HasValue)
            order.EstimatedMinutes = request.EstimatedMinutes.Value;

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Auto-deduct inventory when order is served or completed
        if (status == OrderStatus.Served || status == OrderStatus.Completed)
        {
            try { await _inventoryService.DeductForOrderAsync(id); }
            catch (Exception ex) { Console.WriteLine($"[Inventory] Deduction failed for order {id}: {ex.Message}"); }
        }

        var response = MapOrder(order, order.Table?.TableNumber ?? "");

        await BroadcastOrderUpdatedAsync(response, order.TenantId);

        if (status == OrderStatus.Accepted)
            await _printService.QueueKotAsync(order.Id, order.TenantId, "OnAccept");

        // Sound alert on kitchen for specific statuses
        if (status == OrderStatus.Accepted || status == OrderStatus.Ready)
            await _hub.Clients.Group(order.TenantId).SendAsync("KitchenAlert", new { OrderId = id, Status = status.ToString() });

        return response;
    }

    public async Task<LiveOrdersResponse> GetLiveOrdersAsync()
    {
        var liveStatuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Accepted,
            OrderStatus.Preparing,
            OrderStatus.Ready
        };

        var orders = await GetFullOrderQuery()
            .Where(o => liveStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return new LiveOrdersResponse
        {
            TotalPending = orders.Count(o => o.Status == OrderStatus.Pending),
            TotalPreparing = orders.Count(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Accepted),
            TotalReady = orders.Count(o => o.Status == OrderStatus.Ready),
            Orders = orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList()
        };
    }

    public async Task<List<OrderResponse>> GetOpenTakeawayOrdersAsync()
    {
        var orders = await GetFullOrderQuery()
            .Where(o => o.Type == OrderType.Takeaway
                        && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        return orders.Select(o => MapOrder(o, "")).ToList();
    }

    // ── Room service ──

    public async Task<List<OrderResponse>> GetRoomServiceQueueAsync()
    {
        var orders = await GetFullOrderQuery()
            .Where(o => o.Type == OrderType.RoomService
                        && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        return orders.Select(o => MapOrder(o, "")).ToList();
    }

    public async Task<List<OrderResponse>> GetStayOrdersAsync(Guid stayId)
    {
        var orders = await GetFullOrderQuery()
            .Where(o => o.StayId == stayId && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        return orders.Select(o => MapOrder(o, "")).ToList();
    }

    /// <summary>Attendant took the order from the kitchen: it leaves the kitchen screen.</summary>
    public async Task<OrderResponse> DispatchRoomOrderAsync(Guid orderId)
    {
        var order = await LoadRoomOrderAsync(orderId);
        if (order.DispatchedAt != null) return MapOrder(order, "");

        var now = DateTime.UtcNow;
        var deduct = order.ServedAt == null;
        order.DispatchedAt = now;
        order.ServedAt ??= now;
        order.PreparedAt ??= now;
        order.Status = OrderStatus.Served;
        await _db.SaveChangesAsync();

        if (deduct)
        {
            try { await _inventoryService.DeductForOrderAsync(order.Id); }
            catch (Exception ex) { Console.WriteLine($"[Inventory] Deduction failed for order {order.Id}: {ex.Message}"); }
        }

        var response = MapOrder(order, "");
        await BroadcastOrderUpdatedAsync(response, order.TenantId);
        return response;
    }

    /// <summary>
    /// Handed to the guest. Room-bill orders (and ones already paid) are done; pay-on-delivery ones
    /// stay open until the attendant's payment is billed.
    /// </summary>
    public async Task<OrderResponse> DeliverRoomOrderAsync(Guid orderId)
    {
        var order = await LoadRoomOrderAsync(orderId);
        if (order.DispatchedAt == null)
            await DispatchRoomOrderAsync(orderId);

        order.DeliveredAt ??= DateTime.UtcNow;
        if (order.ChargeToRoom || order.BillNumber != null)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt ??= order.DeliveredAt;
        }
        await _db.SaveChangesAsync();

        var response = MapOrder(order, "");
        await BroadcastOrderUpdatedAsync(response, order.TenantId);
        return response;
    }

    private async Task<Order> LoadRoomOrderAsync(Guid orderId)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");
        if (order.Type != OrderType.RoomService)
            throw new InvalidOperationException("Not a room service order.");
        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("This order was cancelled.");
        return order;
    }

    /// <summary>
    /// Checkout: one bill for everything the stay still owes (room charges and any unpaid room
    /// service). Null when there's nothing to bill.
    /// </summary>
    public async Task<TableSessionSummary?> SettleStayAsync(Guid stayId, ClearTableRequest request)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.StayId == stayId && o.BillNumber == null && o.Status != OrderStatus.Cancelled)
            .ToListAsync();
        if (orders.Count == 0) return null;

        var (billNumber, paidAmount, dueAmount, loyalty) = await SettleAsync(orders, request, orders[0].TenantId, completeOrders: true);
        foreach (var o in orders)
            await BroadcastOrderUpdatedAsync(MapOrder(o, ""), o.TenantId);
        return await BuildSettledSummaryAsync(billNumber, null, paidAmount, dueAmount, 0, loyalty);
    }

    public async Task<LiveOrdersResponse> GetKitchenOrdersAsync()
    {
        var kitchenStatuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Accepted,
            OrderStatus.Preparing,
            OrderStatus.Ready
        };

        var orders = await GetFullOrderQuery()
            .Where(o => kitchenStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return new LiveOrdersResponse
        {
            TotalPending = 0,
            TotalPreparing = orders.Count(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Accepted),
            TotalReady = orders.Count(o => o.Status == OrderStatus.Ready),
            Orders = orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList()
        };
    }

    public async Task<List<OrderResponse>> GetOrdersByTableAsync(Guid tableId, string? customerSessionId = null)
    {
        var mainTableId = await ResolveMainTableIdAsync(tableId);

        // With a customer session, return that session's orders even if staff merged or
        // transferred the table since they ordered
        var query = string.IsNullOrEmpty(customerSessionId)
            ? GetFullOrderQuery().Where(o => o.TableId == mainTableId)
            : GetFullOrderQuery().Where(o => o.CustomerSessionId == customerSessionId);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .ToListAsync();

        return orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList();
    }

    public async Task<OrderResponse?> GetOrderByNumberAsync(string orderNumber)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        if (order == null) return null;
        return MapOrder(order, order.Table?.TableNumber ?? "");
    }

    private IQueryable<Order> GetFullOrderQuery()
    {
        return _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Include(o => o.Payment);
    }

    private static OrderResponse MapOrder(Order order, string tableNumber) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        // Takeaway/room orders have no table: show their token instead
        TableNumber = order.TableId == null ? (order.TokenNumber ?? tableNumber) : tableNumber,
        TableId = order.TableId,
        Status = order.Status.ToString(),
        Type = order.Type.ToString(),
        SubTotal = order.SubTotal,
        Tax = order.Tax,
        DiscountAmount = order.DiscountAmount,
        TotalAmount = order.TotalAmount,
        SpecialInstructions = order.SpecialInstructions,
        EstimatedMinutes = order.EstimatedMinutes,
        CreatedAt = order.CreatedAt,
        AcceptedAt = order.AcceptedAt,
        PreparedAt = order.PreparedAt,
        ServedAt = order.ServedAt,
        CompletedAt = order.CompletedAt,
        BillNumber = order.BillNumber,
        CustomerSessionId = order.CustomerSessionId,
        PackagingCharge = order.PackagingCharge,
        LoyaltyDiscount = order.LoyaltyDiscount,
        TokenNumber = order.TokenNumber,
        CustomerName = order.CustomerName,
        CustomerMobile = order.CustomerMobile,
        PickedUpAt = order.PickedUpAt,
        RoomId = order.RoomId,
        StayId = order.StayId,
        ChargeToRoom = order.ChargeToRoom,
        DispatchedAt = order.DispatchedAt,
        DeliveredAt = order.DeliveredAt,
        Items = order.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            MenuItemId = i.MenuItemId,
            ItemName = i.ItemName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Modifiers = i.Modifiers,
            Notes = i.Notes,
            IsParcel = i.IsParcel,
            PackagingCharge = i.PackagingCharge
        }).ToList(),
        Payment = order.Payment != null ? new PaymentSummary
        {
            Method = order.Payment.Method.ToString(),
            Status = order.Payment.Status.ToString(),
            PaidAt = order.Payment.PaidAt
        } : null
    };

    // ── Table Session (all active orders at a table) ──

    private static readonly OrderStatus[] ActiveStatuses = new[]
    {
        OrderStatus.Pending, OrderStatus.Accepted,
        OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served
    };

    public async Task<TableSessionSummary> GetTableSessionAsync(Guid tableId)
    {
        var table = await _db.Tables.FindAsync(await ResolveMainTableIdAsync(tableId))
            ?? throw new KeyNotFoundException("Table not found.");
        tableId = table.Id;

        var orders = await GetFullOrderQuery()
            .Where(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var mapped = orders.Select(o => MapOrder(o, table.TableNumber)).ToList();

        return new TableSessionSummary
        {
            TableId = table.Id,
            TableNumber = table.TableNumber,
            TableLabel = table.Label,
            ActiveOrderCount = mapped.Count,
            GrandSubTotal = mapped.Sum(o => o.SubTotal),
            GrandPackaging = mapped.Sum(o => o.PackagingCharge),
            GrandLoyaltyDiscount = mapped.Sum(o => o.LoyaltyDiscount),
            GrandTax = mapped.Sum(o => o.Tax),
            GrandDiscount = mapped.Sum(o => o.DiscountAmount),
            GrandTotal = mapped.Sum(o => o.TotalAmount),
            Orders = mapped
        };
    }

    /// <summary>A table merged into another resolves to its main table (which holds the bill).</summary>
    private async Task<Guid> ResolveMainTableIdAsync(Guid tableId)
    {
        var mainId = await _db.Tables
            .IgnoreQueryFilters()
            .Where(t => t.Id == tableId && !t.IsDeleted)
            .Select(t => t.MergedIntoTableId)
            .FirstOrDefaultAsync();
        return mainId ?? tableId;
    }

    public async Task<TableSessionSummary> ClearTableAsync(Guid tableId, ClearTableRequest request)
    {
        tableId = await ResolveMainTableIdAsync(tableId);
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");

        var activeOrders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status))
            .ToListAsync();

        // Billing one party at a shared table: only their orders
        if (request.OrderIds is { Count: > 0 })
        {
            activeOrders = activeOrders.Where(o => request.OrderIds.Contains(o.Id)).ToList();
            if (activeOrders.Count == 0)
                throw new InvalidOperationException("These orders are already billed.");
        }

        var (billNumber, paidAmount, dueAmount, loyalty) = await SettleAsync(activeOrders, request, table.TenantId, completeOrders: true);

        var anyLeft = await _db.Orders.AnyAsync(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status));
        table.BillRequestedAt = null;
        table.BillRequestedBy = null;

        if (!anyLeft)
        {
            table.IsCallingWaiter = false;
            table.WaiterCalledAt = null;
            await _db.SaveChangesAsync();

            // Free the waitlist party seated here, and un-merge tables merged into this one
            await _waitlistService.ReleaseTableAsync(tableId);
            var merged = await _db.Tables.Where(t => t.MergedIntoTableId == tableId).ToListAsync();
            if (merged.Count > 0)
            {
                foreach (var t in merged) t.MergedIntoTableId = null;
                await _db.SaveChangesAsync();
            }

            // Staff screens, plus customers still on the menu at this table (or a table merged into it)
            await _hub.Clients.Group(table.TenantId)
                .SendAsync("TableCleared", tableId.ToString(), table.TableNumber);
            await _hub.Clients.Groups(merged.Select(t => $"table-{t.Id}").Append($"table-{tableId}").ToList())
                .SendAsync("TableSessionEnded");
        }
        else
        {
            // Other parties are still eating: only the billed party's phones end their session
            await _db.SaveChangesAsync();
            var sessions = activeOrders.Select(o => o.CustomerSessionId).OfType<string>().Distinct().ToList();
            var mergedIds = await _db.Tables.Where(t => t.MergedIntoTableId == tableId).Select(t => t.Id).ToListAsync();
            if (sessions.Count > 0)
                await _hub.Clients.Groups(mergedIds.Select(id => $"table-{id}").Append($"table-{tableId}").ToList())
                    .SendAsync("PartySettled", sessions);
        }
        await _hub.BroadcastTablesChangedAsync(table.TenantId);

        return await BuildSettledSummaryAsync(billNumber, table, paidAmount, dueAmount, anyLeft ? await CountActiveAsync(tableId) : 0, loyalty);
    }

    public async Task<TableSessionSummary> SettleOrdersAsync(ClearTableRequest request)
    {
        if (request.OrderIds is not { Count: > 0 })
            throw new ArgumentException("Choose the orders to bill.");

        // Unbilled table-less orders are few; filter the ids in memory (the MySQL provider can't translate list Contains)
        var orders = (await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.TableId == null && o.BillNumber == null && o.Status != OrderStatus.Cancelled)
            .ToListAsync())
            .Where(o => request.OrderIds.Contains(o.Id))
            .ToList();
        if (orders.Count == 0)
            throw new InvalidOperationException("These orders are already billed.");

        // Takeaway is usually paid before it's cooked: billing doesn't close the order; pickup does
        var (billNumber, paidAmount, dueAmount, loyalty) = await SettleAsync(orders, request, orders[0].TenantId, completeOrders: false);

        // Room service paid on delivery: once it's delivered and paid, it's done
        var delivered = orders.Where(o => o.Type == OrderType.RoomService && o.DeliveredAt != null).ToList();
        foreach (var o in delivered) o.Status = OrderStatus.Completed;
        if (delivered.Count > 0) await _db.SaveChangesAsync();

        foreach (var o in orders)
            await BroadcastOrderUpdatedAsync(MapOrder(o, ""), o.TenantId);

        return await BuildSettledSummaryAsync(billNumber, null, paidAmount, dueAmount, 0, loyalty);
    }

    public async Task<OrderResponse> MarkPickedUpAsync(Guid orderId)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");
        if (order.Type != OrderType.Takeaway)
            throw new InvalidOperationException("Only takeaway orders are picked up.");
        if (order.BillNumber == null)
            throw new InvalidOperationException("Settle the bill before handing over the order.");
        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("This order was cancelled.");

        var now = DateTime.UtcNow;
        var deduct = order.ServedAt == null && order.Status != OrderStatus.Completed;
        order.PickedUpAt = now;
        order.ServedAt ??= now;
        order.Status = OrderStatus.Completed;
        await _db.SaveChangesAsync();

        if (deduct)
        {
            try { await _inventoryService.DeductForOrderAsync(order.Id); }
            catch (Exception ex) { Console.WriteLine($"[Inventory] Deduction failed for order {order.Id}: {ex.Message}"); }
        }

        var response = MapOrder(order, "");
        await BroadcastOrderUpdatedAsync(response, order.TenantId);
        return response;
    }

    private Task<int> CountActiveAsync(Guid tableId) =>
        _db.Orders.CountAsync(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status));

    /// <summary>
    /// Gives the orders one bill number and applies the discount, payment, udhaar and customer
    /// record. <paramref name="completeOrders"/>: dine-in bills close the orders (and use up stock
    /// for anything not served yet); takeaway bills leave them open until pickup.
    /// </summary>
    private async Task<(string BillNumber, decimal PaidAmount, decimal DueAmount, LoyaltyOutcome? Loyalty)> SettleAsync(
        List<Order> orders, ClearTableRequest request, string tenantId, bool completeOrders)
    {
        var billNumber = await NextBillNumberAsync(tenantId);
        var now = DateTime.UtcNow;
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);

        var customerName = request.CustomerName ?? orders.Select(o => o.CustomerName).FirstOrDefault(n => n != null);
        var customerMobile = request.CustomerMobile ?? orders.Select(o => o.CustomerMobile).FirstOrDefault(m => m != null);

        // Customer record (visit stats, loyalty) for anyone billed with a mobile number
        var customer = (tenant.LoyaltyEnabled || !string.IsNullOrWhiteSpace(customerName))
            ? await _loyalty.FindOrCreateAsync(tenantId, customerName, customerMobile, request.CustomerDateOfBirth)
            : null;

        // Loyalty points come off before tax (they reduce the taxable value)
        var redeemed = (Points: 0, Value: 0m);
        if (request.RedeemPoints > 0)
        {
            if (customer == null)
                throw new InvalidOperationException("Enter the customer's mobile number to use their points.");
            var preTax = orders.Sum(o => o.SubTotal + o.PackagingCharge - o.LoyaltyDiscount);
            redeemed = _loyalty.PlanRedemption(tenant, customer, request.RedeemPoints, preTax);
            ApplyPreTaxDiscount(orders, redeemed.Value);
        }

        // Distribute discount proportionally across orders
        var totalBeforeDiscount = orders.Sum(o => o.TotalAmount);
        var discount = Math.Max(0, Math.Min(request.DiscountAmount, totalBeforeDiscount));

        foreach (var order in orders)
        {
            if (completeOrders) order.Status = OrderStatus.Completed;
            order.CompletedAt = now;
            order.UpdatedAt = now;
            order.BillNumber = billNumber;

            if (discount > 0 && totalBeforeDiscount > 0)
            {
                // Proportional discount per order (add to any existing wheel discount)
                var proportion = order.TotalAmount / totalBeforeDiscount;
                var additionalDiscount = Math.Round(discount * proportion, 2);
                order.DiscountAmount += additionalDiscount;
                order.TotalAmount -= additionalDiscount;
            }
        }

        // Handle partial payment / dues (udhaar)
        var finalTotal = orders.Sum(o => o.TotalAmount);
        // A payment split (cash/UPI/card…) says exactly what was paid now; the rest is udhaar
        var splitTotal = request.Payments?.Where(p => p.Amount > 0).Sum(p => p.Amount) ?? 0;
        if (splitTotal > finalTotal + 1)
            throw new InvalidOperationException($"Payments (₹{splitTotal:0.##}) are more than the bill (₹{finalTotal:0.##}).");
        var paidAmount = splitTotal > 0 ? Math.Min(splitTotal, finalTotal)
            : request.PaidAmount > 0 ? Math.Min(request.PaidAmount, finalTotal) : finalTotal;
        var dueAmount = finalTotal - paidAmount;
        if (dueAmount > 0.01m && string.IsNullOrWhiteSpace(customerName))
            throw new InvalidOperationException("Enter the customer's name to put the rest on udhaar.");
        if (paidAmount > 0)
            _accounting.RecordBillPayments(tenant, billNumber, splitTotal > 0 ? request.Payments : null, paidAmount);

        if (dueAmount > 0 && !string.IsNullOrWhiteSpace(customerName))
        {
            _db.CustomerDues.Add(new Models.CustomerDue
            {
                TenantId = tenantId,
                CustomerName = customerName.Trim(),
                CustomerMobile = customerMobile?.Trim(),
                BillNumber = billNumber,
                BillAmount = finalTotal,
                PaidAmount = paidAmount,
                DueAmount = dueAmount,
                Notes = request.Notes
            });
        }

        LoyaltyOutcome? loyalty = null;
        if (customer != null)
        {
            if (redeemed.Points > 0) _loyalty.RecordRedemption(customer, redeemed.Points, billNumber);
            // Earn on the paid part, before tax; the udhaar part earns when it's paid
            var (earned, bonus) = await _loyalty.EarnAsync(tenant, customer, paidAmount * PreTaxShare(orders), billNumber);
            LoyaltyService.RecordVisit(customer, finalTotal);
            if (tenant.LoyaltyEnabled)
                loyalty = new LoyaltyOutcome
                {
                    PointsRedeemed = redeemed.Points, RedeemedValue = redeemed.Value,
                    PointsEarned = earned, BonusPoints = bonus, Balance = customer.PointsBalance
                };
        }

        await _db.SaveChangesAsync();

        // Auto-deduct inventory for orders that weren't already deducted at Served status
        if (completeOrders)
        {
            foreach (var order in orders.Where(o => o.ServedAt == null))
            {
                try { await _inventoryService.DeductForOrderAsync(order.Id); }
                catch (Exception ex) { Console.WriteLine($"[Inventory] Deduction failed for order {order.Id}: {ex.Message}"); }
            }
        }

        return (billNumber, paidAmount, dueAmount, loyalty);
    }

    /// <summary>Takes <paramref name="value"/> off the orders' taxable amount (shared in proportion), re-taxing each.</summary>
    private static void ApplyPreTaxDiscount(List<Order> orders, decimal value)
    {
        var bases = orders.Select(o => o.SubTotal + o.PackagingCharge - o.LoyaltyDiscount).ToList();
        var total = bases.Sum();
        if (total <= 0 || value <= 0) return;

        var left = value;
        for (var i = 0; i < orders.Count; i++)
        {
            var o = orders[i];
            var share = i == orders.Count - 1 ? left : Math.Round(value * bases[i] / total, 2);
            share = Math.Min(share, bases[i]);
            left -= share;

            var rate = bases[i] > 0 ? o.Tax / bases[i] : 0m;
            o.LoyaltyDiscount += share;
            var taxable = bases[i] - share;
            o.Tax = Math.Round(taxable * rate, 2);
            o.TotalAmount = taxable + o.Tax - o.DiscountAmount;
        }
    }

    /// <summary>Part of the orders' total that is before tax (points are earned on that part).</summary>
    private static decimal PreTaxShare(List<Order> orders)
    {
        var preTax = orders.Sum(o => o.SubTotal + o.PackagingCharge - o.LoyaltyDiscount);
        var withTax = preTax + orders.Sum(o => o.Tax);
        return withTax > 0 ? preTax / withTax : 1m;
    }

    /// <summary>BILL-YYYYMMDD-XXXX (restaurant-local day; the sequence runs across days).</summary>
    private async Task<string> NextBillNumberAsync(string tenantId)
    {
        var today = BusinessClock.Today.ToString("yyyyMMdd");
        var billCount = await _db.Orders
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.BillNumber != null)
            .Select(o => o.BillNumber)
            .Distinct()
            .CountAsync();
        return $"BILL-{today}-{(billCount + 1):D4}";
    }

    /// <summary>The just-settled bill, shaped like a table session so the bill screen can print it.</summary>
    private async Task<TableSessionSummary> BuildSettledSummaryAsync(
        string billNumber, RestaurantTable? table, decimal paidAmount, decimal dueAmount, int stillActive, LoyaltyOutcome? loyalty = null)
    {
        var billed = await GetFullOrderQuery()
            .Where(o => o.BillNumber == billNumber)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        var mapped = billed.Select(o => MapOrder(o, table?.TableNumber ?? "")).ToList();

        return new TableSessionSummary
        {
            TableId = table?.Id,
            TableNumber = table?.TableNumber ?? (mapped.FirstOrDefault()?.TableNumber ?? ""),
            TableLabel = table?.Label,
            ActiveOrderCount = stillActive,
            GrandSubTotal = mapped.Sum(o => o.SubTotal),
            GrandPackaging = mapped.Sum(o => o.PackagingCharge),
            GrandLoyaltyDiscount = mapped.Sum(o => o.LoyaltyDiscount),
            GrandTax = mapped.Sum(o => o.Tax),
            GrandDiscount = mapped.Sum(o => o.DiscountAmount),
            GrandTotal = mapped.Sum(o => o.TotalAmount),
            PaidAmount = paidAmount,
            DueAmount = dueAmount,
            Orders = mapped,
            Loyalty = loyalty
        };
    }

    /// <summary>What a table-less bill is "for" on the bill list: the takeaway token or room.</summary>
    private static string BillLabel(Order o) => o.Type switch
    {
        OrderType.Takeaway => $"Takeaway {o.TokenNumber}".Trim(),
        OrderType.RoomService => $"Room {o.TokenNumber}".Trim(),
        _ => o.TokenNumber ?? ""
    };

    public async Task<BillResponse> GetBillAsync(string billNumber)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.BillNumber == billNumber && o.Status != OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        if (orders.Count == 0)
            throw new KeyNotFoundException("Bill not found.");

        var due = await _db.CustomerDues.FirstOrDefaultAsync(d => d.BillNumber == billNumber);
        var adjustments = await _db.BillAdjustments
            .Where(a => a.BillNumber == billNumber)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        var table = orders[0].Table;
        var total = orders.Sum(o => o.TotalAmount);
        var points = await _db.LoyaltyTransactions.Where(t => t.BillNumber == billNumber).ToListAsync();

        return new BillResponse
        {
            BillNumber = billNumber,
            TableNumber = table?.TableNumber ?? BillLabel(orders[0]),
            TableLabel = table?.Label,
            OrderCount = orders.Count,
            TotalItems = orders.Sum(o => o.Items.Count),
            SubTotal = orders.Sum(o => o.SubTotal),
            PackagingCharge = orders.Sum(o => o.PackagingCharge),
            LoyaltyDiscount = orders.Sum(o => o.LoyaltyDiscount),
            PointsEarned = points.Where(p => p.Points > 0).Sum(p => p.Points),
            PointsRedeemed = -points.Where(p => p.Type == LoyaltyTransactionType.Redeem).Sum(p => p.Points),
            Tax = orders.Sum(o => o.Tax),
            DiscountAmount = orders.Sum(o => o.DiscountAmount),
            TotalAmount = total,
            PaidAmount = due?.PaidAmount ?? total,
            DueAmount = due?.DueAmount ?? 0,
            CustomerName = due?.CustomerName,
            CustomerMobile = due?.CustomerMobile,
            CompletedAt = orders.Max(o => o.CompletedAt ?? o.CreatedAt),
            Orders = orders.Select(o => MapOrder(o, table?.TableNumber ?? "")).ToList(),
            Adjustments = adjustments.Select(MapAdjustment).ToList()
        };
    }

    public async Task<RemoveBillItemsResponse> RemoveBillItemsAsync(string billNumber, RemoveBillItemsRequest request, string? changedBy)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.BillNumber == billNumber && o.Status != OrderStatus.Cancelled)
            .ToListAsync();
        if (orders.Count == 0)
            throw new KeyNotFoundException("Bill not found.");

        var previousTotal = orders.Sum(o => o.TotalAmount);
        var items = orders.SelectMany(o => o.Items).ToDictionary(i => i.Id);

        // Combine repeated lines for the same item, then validate
        var removals = request.Items
            .GroupBy(r => r.OrderItemId)
            .Select(g => (ItemId: g.Key, Quantity: g.Sum(r => r.Quantity)))
            .ToList();
        foreach (var (itemId, quantity) in removals)
        {
            if (!items.TryGetValue(itemId, out var item))
                throw new KeyNotFoundException("An item isn't on this bill any more. Refresh and try again.");
            if (quantity > item.Quantity)
                throw new InvalidOperationException($"Only {item.Quantity} × {item.ItemName} on the bill.");
        }
        var remainingQty = items.Values.Sum(i => i.Quantity) - removals.Sum(r => r.Quantity);
        if (remainingQty <= 0)
            throw new InvalidOperationException("A bill needs at least one item. To void the whole bill, contact the owner.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var restored = new List<(Guid MenuItemId, int Quantity)>();

        foreach (var order in orders)
        {
            var orderRemovals = removals.Where(r => order.Items.Any(i => i.Id == r.ItemId)).ToList();
            if (orderRemovals.Count == 0) continue;

            // Keep the tax rate the bill was charged at (settings may have changed since)
            var taxable = order.SubTotal + order.PackagingCharge - order.LoyaltyDiscount;
            var taxRate = taxable > 0 ? order.Tax / taxable : 0m;
            var discount = order.DiscountAmount;

            foreach (var (itemId, quantity) in orderRemovals)
            {
                var item = order.Items.First(i => i.Id == itemId);
                var lineUnit = item.Quantity > 0 ? item.TotalPrice / item.Quantity : item.UnitPrice;
                var removedValue = Math.Round((lineUnit + item.PackagingCharge) * quantity, 2);

                _db.BillAdjustments.Add(new BillAdjustment
                {
                    TenantId = order.TenantId,
                    BillNumber = billNumber,
                    OrderId = order.Id,
                    ItemName = item.ItemName,
                    QuantityRemoved = quantity,
                    AmountRemoved = Math.Round(removedValue * (1 + taxRate), 2),
                    Reason = reason,
                    ChangedBy = changedBy
                });
                restored.Add((item.MenuItemId, quantity));

                if (quantity >= item.Quantity)
                {
                    _db.Set<OrderItem>().Remove(item);
                    order.Items.Remove(item);
                }
                else
                {
                    item.Quantity -= quantity;
                    item.TotalPrice = Math.Round(lineUnit * item.Quantity, 2);
                    item.UpdatedAt = DateTime.UtcNow;
                }
            }

            ApplyTotals(order, taxRate, subtractDiscount: false);
            order.DiscountAmount = Math.Min(discount, order.TotalAmount);
            order.TotalAmount -= order.DiscountAmount;
            order.UpdatedAt = DateTime.UtcNow;

            // Nothing left on this order: drop it from the bill
            if (order.Items.Count == 0)
                order.Status = OrderStatus.Cancelled;
        }

        var newTotal = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);

        // Udhaar on this bill: shrink what's owed; if they'd already paid more than the new total, it's a refund
        decimal refund;
        var due = await _db.CustomerDues.FirstOrDefaultAsync(d => d.BillNumber == billNumber);
        if (due != null)
        {
            refund = Math.Max(0, due.PaidAmount - newTotal);
            due.BillAmount = newTotal;
            due.PaidAmount = Math.Min(due.PaidAmount, newTotal);
            due.DueAmount = Math.Max(0, newTotal - due.PaidAmount);
            if (due.DueAmount == 0 && !due.IsSettled)
            {
                due.IsSettled = true;
                due.SettledAt = DateTime.UtcNow;
            }
            due.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Paid in full at billing time
            refund = Math.Max(0, previousTotal - newTotal);
        }
        await _accounting.RecordRefundAsync(orders[0].TenantId, billNumber, refund);

        // Points the bill earned shrink with it (on what was actually paid, before tax)
        var kept = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var paidNow = due?.PaidAmount ?? newTotal;
        var tenantForBill = await _db.Tenants.FirstAsync(t => t.TenantId == orders[0].TenantId);
        await _loyalty.ReduceEarnedForBillAsync(tenantForBill, billNumber, kept.Count == 0 ? 0 : paidNow * PreTaxShare(kept));

        await _db.SaveChangesAsync();

        try
        {
            await _inventoryService.RestoreForItemsAsync(restored, $"Returned: removed from bill {billNumber}", changedBy);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Inventory] Restore failed for bill {billNumber}: {ex.Message}");
        }

        return new RemoveBillItemsResponse
        {
            Bill = await GetBillAsync(billNumber),
            PreviousTotal = previousTotal,
            RefundAmount = refund
        };
    }

    private static BillAdjustmentResponse MapAdjustment(BillAdjustment a) => new()
    {
        ItemName = a.ItemName,
        QuantityRemoved = a.QuantityRemoved,
        AmountRemoved = a.AmountRemoved,
        Reason = a.Reason,
        ChangedBy = a.ChangedBy,
        ChangedAt = a.CreatedAt
    };

    public async Task<List<OrderResponse>> GetOrderHistoryAsync(DateTime from, DateTime to)
    {
        // from/to are restaurant-local calendar days (inclusive)
        var (start, end) = BusinessClock.DayRangeUtc(from, to);

        var orders = await GetFullOrderQuery()
            .Where(o => o.Status == OrderStatus.Completed
                     && o.CompletedAt.HasValue
                     && o.CompletedAt.Value >= start
                     && o.CompletedAt.Value < end)
            .OrderByDescending(o => o.CompletedAt)
            .ToListAsync();

        return orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList();
    }

    public async Task<OrderResponse> CancelOrderItemAsync(Guid orderId, Guid itemId)
    {
        var order = await GetFullOrderQuery().FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Served)
            throw new InvalidOperationException("Cannot cancel items on a completed or served order.");

        var item = order.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Order item not found.");

        // Remove the item
        _db.Set<OrderItem>().Remove(item);
        order.Items.Remove(item);

        // Recalculate totals
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == order.TenantId);
        ApplyTotals(order, TaxRate(tenant), subtractDiscount: false);

        // If no items left, cancel the entire order
        if (!order.Items.Any())
        {
            order.Status = OrderStatus.Cancelled;
            order.CompletedAt = DateTime.UtcNow;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var response = MapOrder(order, order.Table?.TableNumber ?? "");

        await BroadcastOrderUpdatedAsync(response, order.TenantId);

        return response;
    }

    public async Task<PaginatedBillsResponse> GetBillsAsync(DateTime from, DateTime to, int page, int pageSize)
    {
        // from/to are restaurant-local calendar days (inclusive)
        var (start, end) = BusinessClock.DayRangeUtc(from, to);

        // Get all completed orders with bill numbers in range
        var billsQuery = _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.Status != OrderStatus.Cancelled
                     && o.BillNumber != null
                     && o.CompletedAt.HasValue
                     && o.CompletedAt.Value >= start
                     && o.CompletedAt.Value < end);

        // Group by BillNumber to get distinct bill count
        var allBillGroups = await billsQuery
            .GroupBy(o => o.BillNumber)
            .Select(g => new
            {
                BillNumber = g.Key!,
                CompletedAt = g.Max(o => o.CompletedAt!.Value),
                SubTotal = g.Sum(o => o.SubTotal),
                PackagingCharge = g.Sum(o => o.PackagingCharge),
                LoyaltyDiscount = g.Sum(o => o.LoyaltyDiscount),
                Tax = g.Sum(o => o.Tax),
                DiscountAmount = g.Sum(o => o.DiscountAmount),
                TotalAmount = g.Sum(o => o.TotalAmount),
                OrderCount = g.Count(),
                TotalItems = g.Sum(o => o.Items.Count)
            })
            .OrderByDescending(g => g.CompletedAt)
            .ToListAsync();

        var totalCount = allBillGroups.Count;
        var totalRevenue = allBillGroups.Sum(b => b.TotalAmount);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Paginate
        var pagedBills = allBillGroups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load full orders for paged bills
        // Note: .Contains() with a local collection is not supported by the MySQL provider,
        // so we filter by bill numbers in memory after loading the date-scoped results.
        var billNumberSet = pagedBills.Select(b => b.BillNumber).ToHashSet();
        var orders = (await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.Status != OrderStatus.Cancelled
                     && o.BillNumber != null
                     && o.CompletedAt.HasValue
                     && o.CompletedAt.Value >= start
                     && o.CompletedAt.Value < end)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync())
            .Where(o => billNumberSet.Contains(o.BillNumber!))
            .ToList();

        // Load customer dues for these bills
        var duesForBills = (await _db.CustomerDues
            .Where(d => d.BillNumber != null)
            .ToListAsync())
            .Where(d => billNumberSet.Contains(d.BillNumber!))
            .GroupBy(d => d.BillNumber!)
            .ToDictionary(g => g.Key, g => g.First());

        var pointsForBills = (await _db.LoyaltyTransactions.Where(t => t.BillNumber != null).ToListAsync())
            .Where(t => billNumberSet.Contains(t.BillNumber!))
            .GroupBy(t => t.BillNumber!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var adjustmentsForBills = (await _db.BillAdjustments.ToListAsync())
            .Where(a => billNumberSet.Contains(a.BillNumber))
            .GroupBy(a => a.BillNumber)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).Select(MapAdjustment).ToList());

        var bills = pagedBills.Select(b =>
        {
            var billOrders = orders.Where(o => o.BillNumber == b.BillNumber).ToList();
            var table = billOrders.FirstOrDefault()?.Table;
            duesForBills.TryGetValue(b.BillNumber, out var due);
            return new BillResponse
            {
                BillNumber = b.BillNumber,
                TableNumber = table?.TableNumber ?? (billOrders.Count > 0 ? BillLabel(billOrders[0]) : ""),
                TableLabel = table?.Label,
                OrderCount = b.OrderCount,
                TotalItems = billOrders.Sum(o => o.Items.Count),
                SubTotal = b.SubTotal,
                PackagingCharge = b.PackagingCharge,
                LoyaltyDiscount = b.LoyaltyDiscount,
                PointsEarned = pointsForBills.GetValueOrDefault(b.BillNumber)?.Where(p => p.Points > 0).Sum(p => p.Points) ?? 0,
                PointsRedeemed = -(pointsForBills.GetValueOrDefault(b.BillNumber)?.Where(p => p.Type == LoyaltyTransactionType.Redeem).Sum(p => p.Points) ?? 0),
                Tax = b.Tax,
                DiscountAmount = b.DiscountAmount,
                TotalAmount = b.TotalAmount,
                PaidAmount = due?.PaidAmount ?? b.TotalAmount,
                DueAmount = due?.DueAmount ?? 0,
                CustomerName = due?.CustomerName,
                CustomerMobile = due?.CustomerMobile,
                CompletedAt = b.CompletedAt,
                Orders = billOrders.Select(o => MapOrder(o, table?.TableNumber ?? "")).ToList(),
                Adjustments = adjustmentsForBills.GetValueOrDefault(b.BillNumber) ?? new()
            };
        }).ToList();

        return new PaginatedBillsResponse
        {
            Bills = bills,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalRevenue = totalRevenue
        };
    }

    public async Task<BillResponse> CreatePastBillAsync(CreatePastBillRequest request)
    {
        var tenantId = _tenantProvider.TenantId;

        var table = await _db.Tables.FindAsync(request.TableId)
            ?? throw new KeyNotFoundException("Table not found.");

        // Generate bill number: BILL-YYYYMMDD-XXXX
        // BillDate is a restaurant-local calendar day; stamp the bill at local noon of that day
        var billDay = DateOnly.FromDateTime(request.BillDate);
        var (billDayStart, billDayEnd) = BusinessClock.DayRangeUtc(billDay);
        var billDate = billDayStart.AddHours(12);
        var dateStr = billDay.ToString("yyyyMMdd");
        var billCount = await _db.Orders
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.BillNumber != null)
            .Select(o => o.BillNumber)
            .Distinct()
            .CountAsync();
        var billNumber = $"BILL-{dateStr}-{(billCount + 1):D4}";

        // Generate order number
        var orderCount = await _db.Orders
            .IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == tenantId && o.CreatedAt >= billDayStart && o.CreatedAt < billDayEnd);
        var orderNumber = $"ORD-{dateStr}-{(orderCount + 1):D4}";

        // Calculate totals
        decimal subTotal = 0;
        var orderItems = new List<OrderItem>();
        foreach (var item in request.Items)
        {
            var menuItem = await _db.MenuItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == item.MenuItemId && m.TenantId == tenantId && !m.IsDeleted)
                ?? throw new KeyNotFoundException($"Menu item {item.MenuItemId} not found.");

            var totalPrice = menuItem.Price * item.Quantity;
            subTotal += totalPrice;
            orderItems.Add(new OrderItem
            {
                TenantId = tenantId,
                ItemName = menuItem.Name,
                MenuItemId = menuItem.Id,
                Quantity = item.Quantity,
                UnitPrice = menuItem.Price,
                TotalPrice = totalPrice
            });
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        var taxRate = tenant != null ? (tenant.CgstPercent + tenant.SgstPercent + tenant.ServiceChargePercent) / 100m : 0.05m;
        var tax = Math.Round(subTotal * taxRate, 2);
        var discount = Math.Max(0, request.DiscountAmount);
        var totalAmount = subTotal + tax - discount;

        var order = new Order
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            TableId = request.TableId,
            Status = OrderStatus.Completed,
            Type = OrderType.DineIn,
            SubTotal = subTotal,
            Tax = tax,
            DiscountAmount = discount,
            TotalAmount = totalAmount,
            BillNumber = billNumber,
            CreatedAt = billDate,
            AcceptedAt = billDate,
            PreparedAt = billDate,
            ServedAt = billDate,
            CompletedAt = billDate,
            Items = orderItems
        };

        _db.Orders.Add(order);

        // Handle dues if partial payment
        var paidAmount = request.PaidAmount > 0 ? Math.Min(request.PaidAmount, totalAmount) : totalAmount;
        var dueAmount = totalAmount - paidAmount;

        if (dueAmount > 0 && !string.IsNullOrWhiteSpace(request.CustomerName))
        {
            _db.CustomerDues.Add(new CustomerDue
            {
                TenantId = tenantId,
                CustomerName = request.CustomerName.Trim(),
                CustomerMobile = request.CustomerMobile?.Trim(),
                BillNumber = billNumber,
                BillAmount = totalAmount,
                PaidAmount = paidAmount,
                DueAmount = dueAmount,
                Notes = request.Notes
            });
        }

        await _db.SaveChangesAsync();

        return new BillResponse
        {
            BillNumber = billNumber,
            TableNumber = table.TableNumber,
            TableLabel = table.Label,
            OrderCount = 1,
            TotalItems = orderItems.Count,
            SubTotal = subTotal,
            Tax = tax,
            DiscountAmount = discount,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            DueAmount = dueAmount,
            CustomerName = request.CustomerName,
            CustomerMobile = request.CustomerMobile,
            CompletedAt = billDate,
            Orders = new List<OrderResponse> { MapOrder(order, table.TableNumber) }
        };
    }

    public async Task<List<OrderResponse>> ApplyWheelDiscountAsync(ApplyWheelDiscountRequest request, string customerSessionId)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        if (!tenant.SpinWheelEnabled || tenant.MaxDiscountPercent <= 0)
            throw new InvalidOperationException("Spin wheel is not enabled.");

        var orders = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.MenuItem)
            .Include(o => o.Table)
            .Where(o => o.TableId == request.TableId
                     && o.CustomerSessionId == customerSessionId
                     && o.Status != OrderStatus.Completed
                     && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        if (orders.Count == 0)
            throw new KeyNotFoundException("No active orders found.");

        // Already has a wheel discount — don't apply again
        if (orders.Any(o => o.DiscountAmount > 0))
            throw new InvalidOperationException("Discount already applied.");

        var totalBefore = orders.Sum(o => o.TotalAmount);

        // Calculate the total discount amount
        decimal totalDiscountAmount;
        if (request.DiscountType == "flat")
        {
            // Flat: cap at max% of total AND the flat value itself
            var maxAllowed = Math.Round(totalBefore * tenant.MaxDiscountPercent / 100m, 2);
            totalDiscountAmount = Math.Min(request.DiscountValue, maxAllowed);
        }
        else
        {
            // Percent: cap at max%
            var pct = Math.Min(request.DiscountValue, tenant.MaxDiscountPercent);
            totalDiscountAmount = Math.Round(totalBefore * pct / 100m, 2);
        }

        if (totalDiscountAmount <= 0) return orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList();

        // Distribute proportionally across orders
        foreach (var order in orders)
        {
            var proportion = totalBefore > 0 ? order.TotalAmount / totalBefore : 0;
            var disc = Math.Round(totalDiscountAmount * proportion, 2);
            order.DiscountAmount += disc;
            order.TotalAmount -= disc;
            if (order.TotalAmount < 0) order.TotalAmount = 0;
            order.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return orders.Select(o => MapOrder(o, o.Table?.TableNumber ?? "")).ToList();
    }
}
