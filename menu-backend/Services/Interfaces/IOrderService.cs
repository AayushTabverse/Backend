using menu_backend.DTOs.Order;

namespace menu_backend.Services.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string tenantId, string? customerSessionId = null);
    Task<OrderResponse?> GetOrderAsync(Guid id);
    Task<OrderResponse> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request);
    Task<LiveOrdersResponse> GetLiveOrdersAsync();
    Task<LiveOrdersResponse> GetKitchenOrdersAsync();
    Task<List<OrderResponse>> GetOrdersByTableAsync(Guid tableId, string? customerSessionId = null);
    Task<OrderResponse?> GetOrderByNumberAsync(string orderNumber);
    Task<TableSessionSummary> GetTableSessionAsync(Guid tableId);
    Task<TableSessionSummary> ClearTableAsync(Guid tableId, ClearTableRequest request);
    Task<List<OrderResponse>> GetOrderHistoryAsync(DateTime from, DateTime to);
    Task<PaginatedBillsResponse> GetBillsAsync(DateTime from, DateTime to, int page, int pageSize);
    Task<BillResponse> GetBillAsync(string billNumber);

    /// <summary>
    /// Removes items from a bill that was already printed (customer disputes an item). Recalculates the
    /// bill at its original tax rate, adjusts any udhaar, puts stock back and records the change.
    /// </summary>
    Task<RemoveBillItemsResponse> RemoveBillItemsAsync(string billNumber, RemoveBillItemsRequest request, string? changedBy);
    Task<OrderResponse> CancelOrderItemAsync(Guid orderId, Guid itemId);
    Task<BillResponse> CreatePastBillAsync(CreatePastBillRequest request);
    Task<List<OrderResponse>> ApplyWheelDiscountAsync(ApplyWheelDiscountRequest request, string customerSessionId);
}
