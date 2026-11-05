using System.Text;
using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Loyalty;
using menu_backend.Helpers;
using menu_backend.Models;
using menu_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.FloorStaff)]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LoyaltyService _loyalty;
    private readonly ITenantProvider _tenantProvider;

    public CustomerController(AppDbContext db, LoyaltyService loyalty, ITenantProvider tenantProvider)
    {
        _db = db;
        _loyalty = loyalty;
        _tenantProvider = tenantProvider;
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
            .Select(c => new { c.CustomerName, c.CustomerMobile, c.PointsBalance })
            .ToListAsync();

        return Ok(ApiResponse<List<object>>.Ok(customers.Cast<object>().ToList()));
    }

    /// <summary>Bill screen: the customer's points and the restaurant's redemption rules.</summary>
    [HttpGet("loyalty")]
    public async Task<IActionResult> LoyaltyLookup([FromQuery] string mobile) =>
        Ok(ApiResponse<LoyaltyLookupResponse>.Ok(await _loyalty.LookupAsync(_tenantProvider.TenantId!, mobile)));

    // ── Customers page (owner/manager) ──

    /// <summary>Customers with visits, spend and points, plus loyalty program totals.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string sort = "recent", [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = Filter(q);
        query = sort switch
        {
            "points" => query.OrderByDescending(c => c.PointsBalance),
            "spend" => query.OrderByDescending(c => c.TotalSpent),
            "visits" => query.OrderByDescending(c => c.VisitCount),
            _ => query.OrderByDescending(c => c.LastVisitAt ?? c.CreatedAt)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(c => MapCustomer(c)).ToListAsync();

        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == _tenantProvider.TenantId);
        var all = _db.Customers.AsQueryable();
        var members = await all.CountAsync();
        var repeat = await all.CountAsync(c => c.VisitCount > 1);
        var outstanding = await all.SumAsync(c => (int?)c.PointsBalance) ?? 0;
        var monthStart = BusinessClock.StartOfDayUtc(new DateOnly(BusinessClock.Today.Year, BusinessClock.Today.Month, 1));
        var redeemedThisMonth = -(await _db.LoyaltyTransactions
            .Where(t => t.Type == LoyaltyTransactionType.Redeem && t.CreatedAt >= monthStart)
            .SumAsync(t => (int?)t.Points) ?? 0);

        return Ok(ApiResponse<CustomerListResponse>.Ok(new CustomerListResponse
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            Members = members,
            PointsOutstanding = outstanding,
            OutstandingValue = outstanding * tenant.LoyaltyPointValue,
            PointsRedeemedThisMonth = redeemedThisMonth,
            RepeatCustomerPercent = members == 0 ? 0 : Math.Round(repeat * 100m / members, 1)
        }));
    }

    [HttpGet("{id}/ledger")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Ledger(Guid id)
    {
        var rows = await _db.LoyaltyTransactions
            .Where(t => t.CustomerId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .Select(t => new LoyaltyTransactionResponse
            {
                Id = t.Id, Type = t.Type.ToString(), Points = t.Points, BalanceAfter = t.BalanceAfter,
                BillNumber = t.BillNumber, Note = t.Note, CreatedBy = t.CreatedBy, CreatedAt = t.CreatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<LoyaltyTransactionResponse>>.Ok(rows));
    }

    /// <summary>Add or remove points by hand (goodwill, correction), with a reason for the ledger.</summary>
    [HttpPost("{id}/adjust-points")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> AdjustPoints(Guid id, [FromBody] AdjustPointsRequest request)
    {
        try
        {
            var balance = await _loyalty.AdjustAsync(id, request.Points, request.Reason);
            return Ok(ApiResponse<int>.Ok(balance, "Points updated."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound(ApiResponse.Fail("Customer not found."));
        customer.CustomerName = request.CustomerName.Trim();
        customer.DateOfBirth = request.DateOfBirth;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CustomerListItem>.Ok(MapCustomer(customer)));
    }

    /// <summary>All customers as CSV (e.g. for a WhatsApp offer).</summary>
    [HttpGet("export")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Export([FromQuery] string? q)
    {
        var rows = await Filter(q).OrderByDescending(c => c.LastVisitAt ?? c.CreatedAt).ToListAsync();
        var csv = new StringBuilder("Name,Mobile,Date of birth,Visits,Total spent,Points,Last visit\n");
        foreach (var c in rows)
            csv.AppendLine(string.Join(",", Csv(c.CustomerName), c.CustomerMobile, c.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                c.VisitCount, c.TotalSpent.ToString("0.00"), c.PointsBalance,
                c.LastVisitAt == null ? "" : BusinessClock.ToLocal(c.LastVisitAt.Value).ToString("yyyy-MM-dd")));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "customers.csv");
    }

    private IQueryable<Customer> Filter(string? q)
    {
        var query = _db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(term) || c.CustomerMobile.Contains(term));
        }
        return query;
    }

    private static CustomerListItem MapCustomer(Customer c) => new()
    {
        Id = c.Id, CustomerName = c.CustomerName, CustomerMobile = c.CustomerMobile, DateOfBirth = c.DateOfBirth,
        PointsBalance = c.PointsBalance, VisitCount = c.VisitCount, TotalSpent = c.TotalSpent,
        LastVisitAt = c.LastVisitAt, CreatedAt = c.CreatedAt
    };

    private static string Csv(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
