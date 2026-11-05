using Microsoft.AspNetCore.SignalR;

namespace menu_backend.Hubs;

/// <summary>Broadcasts that more than one service sends.</summary>
public static class OrderHubExtensions
{
    /// <summary>
    /// A table became free, occupied, merged, transferred or reserved. Staff screens refresh on
    /// "TablesChanged"; waiting customers' table board (waitlist status page) on "TableBoardChanged".
    /// </summary>
    public static async Task BroadcastTablesChangedAsync(this IHubContext<OrderHub> hub, string tenantId)
    {
        await hub.Clients.Group(tenantId).SendAsync("TablesChanged");
        await hub.Clients.Group($"waitlist-queue-{tenantId}").SendAsync("TableBoardChanged");
    }
}
