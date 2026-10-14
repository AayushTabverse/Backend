using Microsoft.AspNetCore.SignalR;

namespace menu_backend.Hubs;

/// <summary>
/// SignalR hub for real-time order notifications.
/// Clients join a group by tenantId to receive tenant-scoped events.
/// 
/// Events:
/// - NewOrder: Fired when a new order is placed
/// - OrderStatusUpdated: Fired when an order status changes
/// - KitchenAlert: Sound alert for the kitchen display
/// </summary>
public class OrderHub : Hub
{
    /// <summary>
    /// Join a tenant group to receive real-time order updates.
    /// Called by kitchen/admin/waiter dashboards after connecting.
    /// </summary>
    public async Task JoinTenantGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
        await Clients.Caller.SendAsync("Joined", $"Connected to tenant {tenantId}");
    }

    /// <summary>
    /// Leave a tenant group.
    /// </summary>
    public async Task LeaveTenantGroup(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantId);
    }

    /// <summary>
    /// Customer can join order-specific group for real-time order tracking.
    /// </summary>
    public async Task TrackOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }

    /// <summary>
    /// Customer joins a table-specific group for call waiter.
    /// </summary>
    public async Task JoinTableGroup(string tableId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"table-{tableId}");
    }

    /// <summary>
    /// Waitlisted customer follows their own entry (status page) for "your table is ready".
    /// </summary>
    public async Task JoinWaitlistEntry(string entryId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"waitlist-{entryId}");
    }

    /// <summary>
    /// Waitlisted customer follows the restaurant's queue so their position stays current.
    /// </summary>
    public async Task JoinWaitlistQueue(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"waitlist-queue-{tenantId}");
    }

    /// <summary>
    /// Customer follows their table booking (accepted / declined).
    /// </summary>
    public async Task JoinReservation(string reservationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"reservation-{reservationId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
