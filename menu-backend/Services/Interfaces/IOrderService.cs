using menu_backend.DTOs.Order;

namespace menu_backend.Services.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string tenantId);
    Task<OrderResponse?> GetOrderAsync(Guid id);
    Task<OrderResponse> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request);
    Task<LiveOrdersResponse> GetLiveOrdersAsync();
    Task<LiveOrdersResponse> GetKitchenOrdersAsync();
    Task<List<OrderResponse>> GetOrdersByTableAsync(Guid tableId);
    Task<OrderResponse?> GetOrderByNumberAsync(string orderNumber);
    Task<TableSessionSummary> GetTableSessionAsync(Guid tableId);
    Task<TableSessionSummary> ClearTableAsync(Guid tableId, ClearTableRequest request);
    Task<List<OrderResponse>> GetOrderHistoryAsync(DateTime from, DateTime to);
    Task<PaginatedBillsResponse> GetBillsAsync(DateTime from, DateTime to, int page, int pageSize);
    Task<OrderResponse> CancelOrderItemAsync(Guid orderId, Guid itemId);
}
