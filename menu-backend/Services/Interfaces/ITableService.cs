using menu_backend.DTOs.Table;

namespace menu_backend.Services.Interfaces;

public interface ITableService
{
    Task<TableResponse> CreateTableAsync(CreateTableRequest request);
    Task<List<TableResponse>> GetTablesAsync();
    Task<TableResponse?> GetTableAsync(Guid id);
    Task<TableResponse> UpdateTableAsync(Guid id, UpdateTableRequest request);
    Task DeleteTableAsync(Guid id);
    Task<byte[]> GenerateQrCodeAsync(Guid tableId, string baseUrl);
    Task<TableResponse> CallWaiterAsync(Guid tableId);
    Task DismissCallAsync(Guid tableId);
    Task AssignTablesToWaiterAsync(Guid waiterId, List<Guid> tableIds);
    Task<WaiterAssignmentResponse> GetWaiterAssignmentAsync(Guid waiterId);
    Task<List<WaiterAssignmentResponse>> GetAllAssignmentsAsync();
}
