using menu_backend.DTOs;
using menu_backend.DTOs.Order;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Place a new order (called by the customer app, no auth required).
    /// Tenant is inferred from the table.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, [FromQuery] string tenantId)
    {
        try
        {
            var customerSession = Request.Headers["X-Customer-Session"].FirstOrDefault();
            var result = await _orderService.CreateOrderAsync(request, tenantId, customerSession);
            return Ok(ApiResponse<OrderResponse>.Ok(result, "Order placed successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get a specific order by ID.
    /// For anonymous customers, only returns orders matching their session.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var result = await _orderService.GetOrderAsync(id);
        if (result == null) return NotFound(ApiResponse.Fail("Order not found."));

        // If the caller is not authenticated, verify session ownership
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            var customerSession = Request.Headers["X-Customer-Session"].FirstOrDefault();
            if (!string.IsNullOrEmpty(result.CustomerSessionId) &&
                result.CustomerSessionId != customerSession)
            {
                return NotFound(ApiResponse.Fail("Order not found."));
            }
        }

        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// Get live orders (for admin/waiter panel — includes Pending).
    /// </summary>
    [HttpGet("live")]
    [Authorize(Roles = "RestaurantAdmin,Kitchen,Waiter,SuperAdmin")]
    public async Task<IActionResult> GetLiveOrders()
    {
        var result = await _orderService.GetLiveOrdersAsync();
        return Ok(ApiResponse<LiveOrdersResponse>.Ok(result));
    }

    /// <summary>
    /// Get kitchen orders — only Accepted, Preparing, Ready (excludes Pending).
    /// </summary>
    [HttpGet("kitchen")]
    [Authorize(Roles = "RestaurantAdmin,Kitchen,SuperAdmin")]
    public async Task<IActionResult> GetKitchenOrders()
    {
        var result = await _orderService.GetKitchenOrdersAsync();
        return Ok(ApiResponse<LiveOrdersResponse>.Ok(result));
    }

    /// <summary>
    /// Update order status (kitchen/waiter).
    /// </summary>
    [HttpPut("status/{id}")]
    [Authorize(Roles = "RestaurantAdmin,Kitchen,Waiter,SuperAdmin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var result = await _orderService.UpdateOrderStatusAsync(id, request);
            return Ok(ApiResponse<OrderResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get orders by table (for customer order tracking).
    /// Optionally filtered by X-Customer-Session header.
    /// </summary>
    [HttpGet("by-table/{tableId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrdersByTable(Guid tableId)
    {
        var customerSession = Request.Headers["X-Customer-Session"].FirstOrDefault();
        var result = await _orderService.GetOrdersByTableAsync(tableId, customerSession);
        return Ok(ApiResponse<List<OrderResponse>>.Ok(result));
    }

    /// <summary>
    /// Track order by order number.
    /// </summary>
    [HttpGet("track/{orderNumber}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackOrder(string orderNumber)
    {
        var result = await _orderService.GetOrderByNumberAsync(orderNumber);
        if (result == null) return NotFound(ApiResponse.Fail("Order not found."));
        return Ok(ApiResponse<OrderResponse>.Ok(result));
    }

    /// <summary>
    /// Get table session — all active orders at a table with grand total.
    /// </summary>
    [HttpGet("table-session/{tableId}")]
    [Authorize(Roles = "RestaurantAdmin,Waiter,SuperAdmin")]
    public async Task<IActionResult> GetTableSession(Guid tableId)
    {
        try
        {
            var result = await _orderService.GetTableSessionAsync(tableId);
            return Ok(ApiResponse<TableSessionSummary>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Clear a table — marks all active orders as Completed (bill paid). Admin only.
    /// </summary>
    [HttpPost("clear-table/{tableId}")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> ClearTable(Guid tableId, [FromBody] ClearTableRequest request)
    {
        try
        {
            var result = await _orderService.ClearTableAsync(tableId, request);
            return Ok(ApiResponse<TableSessionSummary>.Ok(result, "Table cleared. All orders marked completed."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get completed order history filtered by date range. Admin only.
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> GetOrderHistory([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await _orderService.GetOrderHistoryAsync(from, to);
        return Ok(ApiResponse<List<OrderResponse>>.Ok(result));
    }

    /// <summary>
    /// Get paginated bills (grouped orders from cleared tables) filtered by date range.
    /// </summary>
    [HttpGet("bills")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> GetBills([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
        var result = await _orderService.GetBillsAsync(from, to, page, pageSize);
        return Ok(ApiResponse<PaginatedBillsResponse>.Ok(result));
    }

    /// <summary>
    /// Cancel a specific item from an order. Accessible by admin/waiter or the customer.
    /// Only allows cancellation if the order is not yet Served/Completed.
    /// </summary>
    [HttpDelete("{orderId}/items/{itemId}")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelOrderItem(Guid orderId, Guid itemId)
    {
        try
        {
            var result = await _orderService.CancelOrderItemAsync(orderId, itemId);
            return Ok(ApiResponse<OrderResponse>.Ok(result, "Item cancelled successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Download order history as CSV. Admin only.
    /// </summary>
    [HttpGet("history/download")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
    public async Task<IActionResult> DownloadOrderHistory([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var orders = await _orderService.GetOrderHistoryAsync(from, to);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Order Number,Table,Date,Time,Items,Subtotal,Tax,Total,Status");
        foreach (var o in orders)
        {
            var items = string.Join(" | ", o.Items.Select(i => $"{i.Quantity}x {i.ItemName}"));
            var completedDate = o.CompletedAt ?? o.CreatedAt;
            csv.AppendLine($"\"{o.OrderNumber}\",\"{o.TableNumber}\",\"{completedDate:yyyy-MM-dd}\",\"{completedDate:HH:mm}\",\"{items}\",{o.SubTotal},{o.Tax},{o.TotalAmount},\"{o.Status}\"");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"bills-{from:yyyyMMdd}-to-{to:yyyyMMdd}.csv");
    }
}
