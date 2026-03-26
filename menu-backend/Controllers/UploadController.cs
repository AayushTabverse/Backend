using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IBlobStorageService _blobService;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public UploadController(IBlobStorageService blobService)
    {
        _blobService = blobService;
    }

    /// <summary>
    /// Upload an image file to Azure Blob Storage.
    /// </summary>
    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "general")
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "No file provided." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { success = false, message = "File size exceeds 5 MB limit." });

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { success = false, message = "Invalid file type. Allowed: JPEG, PNG, GIF, WebP, SVG." });

        // Sanitize folder name — allow only alphanumeric, hyphens, underscores
        var sanitizedFolder = System.Text.RegularExpressions.Regex.Replace(folder, @"[^a-zA-Z0-9\-_]", "");
        if (string.IsNullOrEmpty(sanitizedFolder)) sanitizedFolder = "general";

        // Sanitize file name
        var safeName = Path.GetFileName(file.FileName);

        using var stream = file.OpenReadStream();
        var url = await _blobService.UploadImageAsync(stream, safeName, file.ContentType, sanitizedFolder);

        return Ok(new { success = true, data = new { url } });
    }
}
