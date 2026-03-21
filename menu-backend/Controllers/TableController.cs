using menu_backend.DTOs;
using menu_backend.DTOs.Table;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin,Waiter")]
public class TableController : ControllerBase
{
    private readonly ITableService _tableService;
    private readonly IConfiguration _config;

    public TableController(ITableService tableService, IConfiguration config)
    {
        _tableService = tableService;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        try
        {
            var result = await _tableService.CreateTableAsync(request);
            return Ok(ApiResponse<TableResponse>.Ok(result, "Table created."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin,Waiter")]
    public async Task<IActionResult> GetTables()
    {
        var result = await _tableService.GetTablesAsync();
        return Ok(ApiResponse<List<TableResponse>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin,Waiter")]
    public async Task<IActionResult> GetTable(Guid id)
    {
        var result = await _tableService.GetTableAsync(id);
        if (result == null) return NotFound(ApiResponse.Fail("Table not found."));
        return Ok(ApiResponse<TableResponse>.Ok(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTable(Guid id, [FromBody] UpdateTableRequest request)
    {
        try
        {
            var result = await _tableService.UpdateTableAsync(id, request);
            return Ok(ApiResponse<TableResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTable(Guid id)
    {
        try
        {
            await _tableService.DeleteTableAsync(id);
            return Ok(ApiResponse.Ok("Table deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Generate QR code image for a table.
    /// </summary>
    [HttpGet("{id}/qr")]
    public async Task<IActionResult> GenerateQr(Guid id)
    {
        try
        {
            var baseUrl = _config["App:BaseUrl"] ?? "https://yourapp.com";
            var qrBytes = await _tableService.GenerateQrCodeAsync(id, baseUrl);
            return File(qrBytes, "image/png", $"table-{id}-qr.png");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Call waiter — customer presses the button from QR menu.
    /// </summary>
    [HttpPost("{id}/call-waiter")]
    [AllowAnonymous]
    public async Task<IActionResult> CallWaiter(Guid id)
    {
        try
        {
            var result = await _tableService.CallWaiterAsync(id);
            return Ok(ApiResponse<TableResponse>.Ok(result, "Waiter has been notified!"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Dismiss waiter call — waiter/admin acknowledges the call.
    /// </summary>
    [HttpPost("{id}/dismiss-call")]
    [Authorize(Roles = "RestaurantAdmin,SuperAdmin,Waiter")]
    public async Task<IActionResult> DismissCall(Guid id)
    {
        try
        {
            await _tableService.DismissCallAsync(id);
            return Ok(ApiResponse.Ok("Call dismissed."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
