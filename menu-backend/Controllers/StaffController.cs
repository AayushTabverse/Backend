using System.Security.Claims;
using menu_backend.DTOs;
using menu_backend.Helpers;
using menu_backend.DTOs.Staff;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Management)]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;
    private readonly IStaffImportService _importService;

    public StaffController(IStaffService staffService, IStaffImportService importService)
    {
        _staffService = staffService;
        _importService = importService;
    }

    // ── Excel import ──

    [HttpGet("import/template")]
    public IActionResult DownloadImportTemplate()
    {
        return File(_importService.BuildTemplate(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "tabverse-staff-template.xlsx");
    }

    /// <summary>
    /// Upload an Excel sheet of staff. With apply=false (default) nothing is saved and the parsed rows
    /// are returned for preview; with apply=true valid rows are imported and error rows skipped.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(ExcelImport.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> ImportStaff(IFormFile? file, [FromQuery] bool updateExisting = true, [FromQuery] bool apply = false)
    {
        if (ExcelImport.ValidateFile(file) is { } fileError)
            return BadRequest(ApiResponse.Fail(fileError));

        try
        {
            await using var stream = file!.OpenReadStream();
            var result = await _importService.ImportAsync(stream, updateExisting, apply, CurrentRole);
            var message = result.Applied ? $"Added {result.CreateCount} and updated {result.UpdateCount} staff." : null;
            return Ok(ApiResponse<StaffImportResult>.Ok(result, message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Attendance for all payroll staff on a given day (unmarked staff have a null status).
    /// </summary>
    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance([FromQuery] DateOnly date)
    {
        var attendance = await _staffService.GetAttendanceAsync(date, CurrentRole);
        return Ok(ApiResponse<List<DailyAttendanceResponse>>.Ok(attendance));
    }

    /// <summary>
    /// Bulk upsert attendance for a day. A null status clears that staff member's entry.
    /// </summary>
    [HttpPut("attendance")]
    public async Task<IActionResult> SaveAttendance([FromBody] SaveAttendanceRequest request)
    {
        try
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var attendance = await _staffService.SaveAttendanceAsync(request, CurrentRole, userName);
            return Ok(ApiResponse<List<DailyAttendanceResponse>>.Ok(attendance, "Attendance saved."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpGet("salary")]
    public async Task<IActionResult> GetMonthlySalary([FromQuery] int year, [FromQuery] int month)
    {
        try
        {
            var salary = await _staffService.GetMonthlySalaryAsync(year, month);
            return Ok(ApiResponse<List<MonthlySalaryResponse>>.Ok(salary));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpPut("{userId}/salary")]
    public async Task<IActionResult> UpdateSalary(Guid userId, [FromBody] UpdateSalaryRequest request)
    {
        try
        {
            await _staffService.UpdateSalaryAsync(userId, request.MonthlySalary);
            return Ok(ApiResponse.Ok("Salary updated."));
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

    [Authorize(Roles = Roles.Owner)]
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId)
    {
        try
        {
            var payments = await _staffService.GetPaymentsAsync(year, month, userId);
            return Ok(ApiResponse<List<StaffPaymentResponse>>.Ok(payments));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [Authorize(Roles = Roles.Owner)]
    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment([FromBody] CreateStaffPaymentRequest request)
    {
        try
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var payment = await _staffService.CreatePaymentAsync(request, userName);
            return Ok(ApiResponse<StaffPaymentResponse>.Ok(payment, "Payment recorded."));
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

    [Authorize(Roles = Roles.Owner)]
    [HttpDelete("payments/{id}")]
    public async Task<IActionResult> DeletePayment(Guid id)
    {
        try
        {
            await _staffService.DeletePaymentAsync(id);
            return Ok(ApiResponse.Ok("Payment deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    private UserRole CurrentRole => Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);
}
