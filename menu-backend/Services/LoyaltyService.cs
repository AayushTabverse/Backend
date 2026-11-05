using System.Security.Claims;
using menu_backend.Data;
using menu_backend.DTOs.Loyalty;
using menu_backend.Helpers;
using menu_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

/// <summary>
/// Loyalty points: customers (by mobile) earn on what they pay and redeem at the counter. Every change
/// is a ledger row; Customer.PointsBalance is kept equal to the ledger's sum.
/// </summary>
public class LoyaltyService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public LoyaltyService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private string? StaffName => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    public static string? NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        if (digits.Length > 10) digits = digits[^10..];
        return digits.Length == 10 ? digits : null;
    }

    public async Task<LoyaltyLookupResponse> LookupAsync(string tenantId, string? mobile)
    {
        var tenant = await _db.Tenants.FirstAsync(t => t.TenantId == tenantId);
        var normalized = NormalizeMobile(mobile);
        var customer = normalized == null ? null : await _db.Customers.FirstOrDefaultAsync(c => c.CustomerMobile == normalized);
        return new LoyaltyLookupResponse
        {
            Enabled = tenant.LoyaltyEnabled,
            CustomerId = customer?.Id,
            CustomerName = customer?.CustomerName,
            PointsBalance = customer?.PointsBalance ?? 0,
            PointValue = tenant.LoyaltyPointValue,
            MinRedeemPoints = tenant.LoyaltyMinRedeemPoints,
            MaxRedeemPercent = tenant.LoyaltyMaxRedeemPercent,
            HasDateOfBirth = customer?.DateOfBirth != null,
            BirthdayBonusOn = tenant.LoyaltyEnabled && tenant.LoyaltyBirthdayBonus > 0,
            VisitCount = customer?.VisitCount ?? 0
        };
    }

    /// <summary>The customer with this mobile (created if new). Name is updated when given.</summary>
    public async Task<Customer?> FindOrCreateAsync(string tenantId, string? name, string? mobile, DateOnly? dateOfBirth = null)
    {
        var normalized = NormalizeMobile(mobile);
        if (normalized == null) return null;

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerMobile == normalized);
        if (customer == null)
        {
            customer = new Customer
            {
                TenantId = tenantId,
                CustomerMobile = normalized,
                CustomerName = string.IsNullOrWhiteSpace(name) ? "Guest" : name.Trim()
            };
            _db.Customers.Add(customer);
        }
        else if (!string.IsNullOrWhiteSpace(name))
        {
            customer.CustomerName = name.Trim();
        }
        if (dateOfBirth != null) customer.DateOfBirth = dateOfBirth;
        return customer;
    }

    /// <summary>
    /// How many of the requested points this bill can take (rupee value capped at the restaurant's
    /// share of the pre-tax amount). Throws when the customer can't redeem yet.
    /// </summary>
    public (int Points, decimal Value) PlanRedemption(Tenant tenant, Customer customer, int requested, decimal preTaxAmount)
    {
        if (!tenant.LoyaltyEnabled)
            throw new InvalidOperationException("Loyalty points are switched off.");
        if (customer.PointsBalance < tenant.LoyaltyMinRedeemPoints)
            throw new InvalidOperationException($"{customer.CustomerName} needs at least {tenant.LoyaltyMinRedeemPoints} points to redeem (has {customer.PointsBalance}).");

        var maxValue = Math.Round(preTaxAmount * tenant.LoyaltyMaxRedeemPercent / 100m, 2);
        var maxPoints = tenant.LoyaltyPointValue > 0 ? (int)Math.Floor(maxValue / tenant.LoyaltyPointValue) : 0;
        var points = Math.Min(Math.Min(requested, customer.PointsBalance), maxPoints);
        if (points <= 0)
            throw new InvalidOperationException("This bill is too small to use points on.");
        return (points, Math.Round(points * tenant.LoyaltyPointValue, 2));
    }

    public void RecordRedemption(Customer customer, int points, string billNumber) =>
        Add(customer, LoyaltyTransactionType.Redeem, -points, billNumber, "Redeemed on bill");

    /// <summary>
    /// Points for <paramref name="paidPreTax"/> (the paid part of a bill, before tax), plus the welcome
    /// and birthday bonuses when due. Respects the daily per-number limit.
    /// </summary>
    public async Task<(int Earned, int Bonus)> EarnAsync(Tenant tenant, Customer customer, decimal paidPreTax, string billNumber, string note = "Earned on bill")
    {
        if (!tenant.LoyaltyEnabled) return (0, 0);

        var earned = (int)Math.Floor(paidPreTax / 100m * tenant.LoyaltyPointsPer100);
        if (tenant.LoyaltyDailyEarnCap > 0 && earned > 0)
        {
            var since = BusinessClock.StartOfDayUtc(BusinessClock.Today);
            var today = customer.Id == Guid.Empty ? 0 : await _db.LoyaltyTransactions
                .Where(t => t.CustomerId == customer.Id && t.CreatedAt >= since && t.Type == LoyaltyTransactionType.Earn)
                .SumAsync(t => (int?)t.Points) ?? 0;
            earned = Math.Max(0, Math.Min(earned, tenant.LoyaltyDailyEarnCap - today));
        }
        if (earned > 0) Add(customer, LoyaltyTransactionType.Earn, earned, billNumber, note);

        var bonus = 0;
        if (tenant.LoyaltyWelcomeBonus > 0 && customer.VisitCount == 0 && !await HasHistoryAsync(customer))
        {
            Add(customer, LoyaltyTransactionType.Bonus, tenant.LoyaltyWelcomeBonus, billNumber, "Welcome bonus");
            bonus += tenant.LoyaltyWelcomeBonus;
        }
        if (tenant.LoyaltyBirthdayBonus > 0 && IsBirthdayWeek(customer.DateOfBirth) && customer.LastBirthdayBonusYear != BusinessClock.Today.Year)
        {
            Add(customer, LoyaltyTransactionType.Bonus, tenant.LoyaltyBirthdayBonus, billNumber, "Birthday bonus");
            customer.LastBirthdayBonusYear = BusinessClock.Today.Year;
            bonus += tenant.LoyaltyBirthdayBonus;
        }
        return (earned, bonus);
    }

    /// <summary>Counts a settled bill in the customer's visit stats.</summary>
    public static void RecordVisit(Customer customer, decimal billTotal)
    {
        customer.VisitCount++;
        customer.TotalSpent += billTotal;
        customer.LastVisitAt = DateTime.UtcNow;
    }

    /// <summary>
    /// A bill was reduced after printing: take back the points it earned above what the new amount
    /// earns (never below zero balance).
    /// </summary>
    public async Task ReduceEarnedForBillAsync(Tenant tenant, string billNumber, decimal newPaidPreTax)
    {
        if (!tenant.LoyaltyEnabled) return;
        var earnedRows = await _db.LoyaltyTransactions
            .Where(t => t.BillNumber == billNumber && (t.Type == LoyaltyTransactionType.Earn || t.Type == LoyaltyTransactionType.Reverse))
            .ToListAsync();
        if (earnedRows.Count == 0) return;

        var earnedSoFar = earnedRows.Sum(t => t.Points);
        var shouldHave = (int)Math.Floor(newPaidPreTax / 100m * tenant.LoyaltyPointsPer100);
        var takeBack = earnedSoFar - shouldHave;
        if (takeBack <= 0) return;

        var customer = await _db.Customers.FirstAsync(c => c.Id == earnedRows[0].CustomerId);
        takeBack = Math.Min(takeBack, customer.PointsBalance);
        if (takeBack > 0) Add(customer, LoyaltyTransactionType.Reverse, -takeBack, billNumber, "Bill reduced");
    }

    public async Task<int> AdjustAsync(Guid customerId, int points, string reason)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new KeyNotFoundException("Customer not found.");
        if (points == 0) throw new ArgumentException("Enter the points to add or remove.");
        if (customer.PointsBalance + points < 0)
            throw new InvalidOperationException($"{customer.CustomerName} only has {customer.PointsBalance} points.");
        Add(customer, LoyaltyTransactionType.Adjust, points, null, reason.Trim());
        await _db.SaveChangesAsync();
        return customer.PointsBalance;
    }

    /// <summary>Daily: points of customers with no visit for the restaurant's expiry period lapse.</summary>
    public async Task<int> ExpireAsync()
    {
        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.LoyaltyEnabled && t.LoyaltyExpiryMonths > 0)
            .Select(t => new { t.TenantId, t.LoyaltyExpiryMonths })
            .ToListAsync();

        var expired = 0;
        foreach (var t in tenants)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-t.LoyaltyExpiryMonths);
            var lapsed = await _db.Customers.IgnoreQueryFilters()
                .Where(c => c.TenantId == t.TenantId && !c.IsDeleted && c.PointsBalance > 0
                            && (c.LastVisitAt ?? c.CreatedAt) < cutoff)
                .ToListAsync();
            foreach (var c in lapsed)
            {
                Add(c, LoyaltyTransactionType.Expire, -c.PointsBalance, null, $"No visit in {t.LoyaltyExpiryMonths} months");
                expired++;
            }
        }
        if (expired > 0) await _db.SaveChangesAsync();
        return expired;
    }

    private Task<bool> HasHistoryAsync(Customer customer) =>
        customer.Id == Guid.Empty ? Task.FromResult(false) : _db.LoyaltyTransactions.AnyAsync(t => t.CustomerId == customer.Id);

    /// <summary>Within 3 days either side of the birthday (any year).</summary>
    private static bool IsBirthdayWeek(DateOnly? dob)
    {
        if (dob == null) return false;
        var today = BusinessClock.Today;
        for (var offset = -1; offset <= 1; offset++)
        {
            var year = today.Year + offset;
            var day = Math.Min(dob.Value.Day, DateTime.DaysInMonth(year, dob.Value.Month));
            var birthday = new DateOnly(year, dob.Value.Month, day);
            if (Math.Abs(birthday.DayNumber - today.DayNumber) <= 3) return true;
        }
        return false;
    }

    private void Add(Customer customer, LoyaltyTransactionType type, int points, string? billNumber, string note)
    {
        customer.PointsBalance += points;
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            TenantId = customer.TenantId,
            Customer = customer,
            CustomerId = customer.Id,
            Type = type,
            Points = points,
            BalanceAfter = customer.PointsBalance,
            BillNumber = billNumber,
            Note = note,
            CreatedBy = StaffName ?? (type == LoyaltyTransactionType.Expire ? "System" : null)
        });
    }
}
