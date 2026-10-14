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

    /// <summary>Attach tables to a main table for one party. Running orders on them move to the main table.</summary>
    Task<TableResponse> MergeTablesAsync(Guid mainTableId, List<Guid> tableIds);

    /// <summary>On a merged table: detach it. On a main table: detach all its merged tables.</summary>
    Task UnmergeAsync(Guid tableId);

    /// <summary>Move a party (running orders, waitlist hold, merged tables) to a free table.</summary>
    Task<TableResponse> TransferAsync(Guid fromTableId, Guid toTableId);
}
