using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "RestaurantAdmin,SuperAdmin,Waiter")]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Search customers by name or mobile.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(ApiResponse<List<object>>.Ok(new List<object>()));

        var searchTerm = q.Trim().ToLower();
        var customers = await _db.Customers
            .Where(c => c.CustomerName.ToLower().Contains(searchTerm) ||
                        c.CustomerMobile.Contains(searchTerm))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Take(10)
            .Select(c => new { c.CustomerName, c.CustomerMobile })
            .ToListAsync();

        return Ok(ApiResponse<List<object>>.Ok(customers.Cast<object>().ToList()));
    }
}
