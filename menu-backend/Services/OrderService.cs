using menu_backend.Data;
using menu_backend.DTOs.Order;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IPrintService _printService;

    public OrderService(
        AppDbContext db,
        ITenantProvider tenantProvider,
        IHubContext<OrderHub> hub,
        IPrintService printService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _hub = hub;
        _printService = printService;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string tenantId)
    {
        var table = await _db.Tables
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TableId && t.TenantId == tenantId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Table not found.");

        // Generate order number: ORD-YYYYMMDD-XXXX
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var countToday = await _db.Orders
            .IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == tenantId && o.CreatedAt.Date == DateTime.UtcNow.Date);
        var orderNumber = $"ORD-{today}-{(countToday + 1):D4}";

        var order = new Order
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            TableId = request.TableId,
            Status = OrderStatus.Pending,
            SpecialInstructions = request.SpecialInstructions
        };

        decimal subTotal = 0;
        int maxPrepTime = 0;

        foreach (var itemReq in request.Items)
        {
            var menuItem = await _db.MenuItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == itemReq.MenuItemId && m.TenantId == tenantId && !m.IsDeleted)
                ?? throw new KeyNotFoundException($"Menu item {itemReq.MenuItemId} not found.");

            if (!menuItem.IsAvailable)
                throw new InvalidOperationException($"'{menuItem.Name}' is currently unavailable.");

            var totalPrice = menuItem.Price * itemReq.Quantity;
            subTotal += totalPrice;
            maxPrepTime = Math.Max(maxPrepTime, menuItem.PreparationTimeMinutes);

            order.Items.Add(new OrderItem
            {
                TenantId = tenantId,
                MenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                Quantity = itemReq.Quantity,
                UnitPrice = menuItem.Price,
                TotalPrice = totalPrice,
                Modifiers = itemReq.Modifiers,
                Notes = itemReq.Notes
            });
        }

        order.SubTotal = subTotal;
        order.Tax = Math.Round(subTotal * 0.05m, 2); // 5% GST as default
        order.TotalAmount = order.SubTotal + order.Tax;
        order.EstimatedMinutes = maxPrepTime + 5; // buffer

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Create print job
        await _printService.CreatePrintJobAsync(order.Id, tenantId);

        // Notify kitchen via SignalR
        var response = MapOrder(order, table.TableNumber);
        await _hub.Clients.Group(tenantId).SendAsync("NewOrder", response);

        return response;
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

        var response = MapOrder(order, order.Table?.TableNumber ?? "");

        // Notify all connected clients
        await _hub.Clients.Group(order.TenantId).SendAsync("OrderStatusUpdated", response);

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

    public async Task<LiveOrdersResponse> GetKitchenOrdersAsync()
    {
        var kitchenStatuses = new[]
        {
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

    public async Task<List<OrderResponse>> GetOrdersByTableAsync(Guid tableId)
    {
        var orders = await GetFullOrderQuery()
            .Where(o => o.TableId == tableId)
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
        TableNumber = tableNumber,
        TableId = order.TableId,
        Status = order.Status.ToString(),
        Type = order.Type.ToString(),
        SubTotal = order.SubTotal,
        Tax = order.Tax,
        TotalAmount = order.TotalAmount,
        SpecialInstructions = order.SpecialInstructions,
        EstimatedMinutes = order.EstimatedMinutes,
        CreatedAt = order.CreatedAt,
        AcceptedAt = order.AcceptedAt,
        PreparedAt = order.PreparedAt,
        ServedAt = order.ServedAt,
        CompletedAt = order.CompletedAt,
        Items = order.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            MenuItemId = i.MenuItemId,
            ItemName = i.ItemName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Modifiers = i.Modifiers,
            Notes = i.Notes
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
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");

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
            GrandTax = mapped.Sum(o => o.Tax),
            GrandTotal = mapped.Sum(o => o.TotalAmount),
            Orders = mapped
        };
    }

    public async Task<TableSessionSummary> ClearTableAsync(Guid tableId)
    {
        var table = await _db.Tables.FindAsync(tableId)
            ?? throw new KeyNotFoundException("Table not found.");

        var activeOrders = await _db.Orders
            .Where(o => o.TableId == tableId && ActiveStatuses.Contains(o.Status))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var order in activeOrders)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = now;
            order.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();

        // Return final summary (all now completed — return them so UI can show the bill)
        var completed = await GetFullOrderQuery()
            .Where(o => o.TableId == tableId && o.CompletedAt == now)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var mapped = completed.Select(o => MapOrder(o, table.TableNumber)).ToList();

        // Notify via SignalR
        await _hub.Clients.Group(table.TenantId)
            .SendAsync("TableCleared", tableId.ToString(), table.TableNumber);

        return new TableSessionSummary
        {
            TableId = table.Id,
            TableNumber = table.TableNumber,
            TableLabel = table.Label,
            ActiveOrderCount = 0,
            GrandSubTotal = mapped.Sum(o => o.SubTotal),
            GrandTax = mapped.Sum(o => o.Tax),
            GrandTotal = mapped.Sum(o => o.TotalAmount),
            Orders = mapped
        };
    }

    public async Task<List<OrderResponse>> GetOrderHistoryAsync(DateTime from, DateTime to)
    {
        var toEnd = to.Date.AddDays(1); // include entire 'to' day

        var orders = await GetFullOrderQuery()
            .Where(o => o.Status == OrderStatus.Completed
                     && o.CompletedAt.HasValue
                     && o.CompletedAt.Value >= from.Date
                     && o.CompletedAt.Value < toEnd)
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
        order.SubTotal = order.Items.Sum(i => i.TotalPrice);
        order.Tax = Math.Round(order.SubTotal * 0.05m, 2);
        order.TotalAmount = order.SubTotal + order.Tax;

        // If no items left, cancel the entire order
        if (!order.Items.Any())
        {
            order.Status = OrderStatus.Cancelled;
            order.CompletedAt = DateTime.UtcNow;
        }

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var response = MapOrder(order, order.Table?.TableNumber ?? "");

        // Notify via SignalR
        await _hub.Clients.Group(order.TenantId).SendAsync("OrderStatusUpdated", response);

        return response;
    }
}
