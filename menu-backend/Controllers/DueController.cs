using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Due;
using menu_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin")]
public class DueController : ControllerBase
{
    private readonly AppDbContext _db;

    public DueController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Get all unsettled dues.</summary>
    [HttpGet]
    public async Task<IActionResult> GetDues([FromQuery] bool includeSettled = false)
    {
        var query = _db.CustomerDues.AsQueryable();
        if (!includeSettled)
            query = query.Where(d => !d.IsSettled);

        var dues = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return Ok(ApiResponse<List<CustomerDueResponse>>.Ok(dues.Select(MapDue).ToList()));
    }

    /// <summary>Search dues by customer name or mobile.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchDues([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<List<CustomerDueResponse>>.Ok(new List<CustomerDueResponse>()));

        var searchTerm = q.Trim().ToLower();
        var dues = await _db.CustomerDues
            .Where(d => !d.IsSettled &&
                (d.CustomerName.ToLower().Contains(searchTerm) ||
                 (d.CustomerMobile != null && d.CustomerMobile.Contains(searchTerm))))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<CustomerDueResponse>>.Ok(dues.Select(MapDue).ToList()));
    }

    /// <summary>Get unsettled dues for a customer by mobile number (used when adding previous due to new bill).</summary>
    [HttpGet("by-mobile/{mobile}")]
    public async Task<IActionResult> GetDuesByMobile(string mobile)
    {
        var dues = await _db.CustomerDues
            .Where(d => !d.IsSettled && d.CustomerMobile == mobile)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var totalDue = dues.Sum(d => d.DueAmount);
        return Ok(ApiResponse<object>.Ok(new
        {
            Dues = dues.Select(MapDue).ToList(),
            TotalDue = totalDue
        }));
    }

    /// <summary>Settle a due (full or partial payment).</summary>
    [HttpPost("{id}/settle")]
    public async Task<IActionResult> SettleDue(Guid id, [FromBody] SettleDueRequest request)
    {
        var due = await _db.CustomerDues.FindAsync(id);
        if (due == null) return NotFound(ApiResponse.Fail("Due not found."));

        if (request.Amount <= 0)
            return BadRequest(ApiResponse.Fail("Amount must be greater than 0."));

        var payment = Math.Min(request.Amount, due.DueAmount);
        due.PaidAmount += payment;
        due.DueAmount -= payment;
        due.UpdatedAt = DateTime.UtcNow;

        if (due.DueAmount <= 0)
        {
            due.IsSettled = true;
            due.SettledAt = DateTime.UtcNow;
            due.DueAmount = 0;
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CustomerDueResponse>.Ok(MapDue(due), due.IsSettled ? "Due fully settled." : $"₹{payment} received. Remaining: ₹{due.DueAmount}"));
    }

    private static CustomerDueResponse MapDue(CustomerDue d) => new()
    {
        Id = d.Id,
        CustomerName = d.CustomerName,
        CustomerMobile = d.CustomerMobile,
        BillNumber = d.BillNumber,
        BillAmount = d.BillAmount,
        PaidAmount = d.PaidAmount,
        DueAmount = d.DueAmount,
        IsSettled = d.IsSettled,
        SettledAt = d.SettledAt,
        Notes = d.Notes,
        CreatedAt = d.CreatedAt
    };
}
