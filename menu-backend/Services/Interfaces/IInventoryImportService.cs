using menu_backend.DTOs.Inventory;

namespace menu_backend.Services.Interfaces;

public interface IInventoryImportService
{
    /// <summary>Blank template with headers and an instructions sheet.</summary>
    byte[] BuildTemplate();

    /// <summary>
    /// Parses and validates the sheet. With apply = false nothing is saved (preview);
    /// with apply = true valid rows are created/updated in one save and error rows are skipped.
    /// </summary>
    Task<InventoryImportResult> ImportAsync(Stream xlsx, bool updateExisting, bool apply, string? userName);
}
