using menu_backend.DTOs.Staff;
using menu_backend.Models;

namespace menu_backend.Services.Interfaces;

public interface IStaffImportService
{
    /// <summary>Blank template with headers and an instructions sheet.</summary>
    byte[] BuildTemplate();

    /// <summary>
    /// Parses and validates the sheet against what <paramref name="actorRole"/> may do.
    /// With apply = false nothing is saved (preview); with apply = true valid rows are saved.
    /// </summary>
    Task<StaffImportResult> ImportAsync(Stream xlsx, bool updateExisting, bool apply, UserRole actorRole);
}
