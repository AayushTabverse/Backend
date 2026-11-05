using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Loyalty;

/// <summary>What the bill screen shows after a mobile number is typed.</summary>
public class LoyaltyLookupResponse
{
    public bool Enabled { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int PointsBalance { get; set; }
    public decimal PointValue { get; set; }
    public int MinRedeemPoints { get; set; }
    public decimal MaxRedeemPercent { get; set; }
    public bool HasDateOfBirth { get; set; }
    /// <summary>The restaurant gives a birthday bonus (ask for the date of birth if missing).</summary>
    public bool BirthdayBonusOn { get; set; }
    public int VisitCount { get; set; }
}

/// <summary>Loyalty result of settling one bill.</summary>
public class LoyaltyOutcome
{
    public int PointsRedeemed { get; set; }
    public decimal RedeemedValue { get; set; }
    public int PointsEarned { get; set; }
    public int BonusPoints { get; set; }
    public int Balance { get; set; }
}

public class CustomerListItem
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public int PointsBalance { get; set; }
    public int VisitCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerListResponse
{
    public List<CustomerListItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    // Program summary
    public int Members { get; set; }
    public int PointsOutstanding { get; set; }
    /// <summary>Outstanding points in rupees (what the restaurant owes in rewards).</summary>
    public decimal OutstandingValue { get; set; }
    public int PointsRedeemedThisMonth { get; set; }
    /// <summary>Share of customers with more than one visit, 0–100.</summary>
    public decimal RepeatCustomerPercent { get; set; }
}

public class LoyaltyTransactionResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? BillNumber { get; set; }
    public string? Note { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdjustPointsRequest
{
    /// <summary>Positive adds, negative removes.</summary>
    [Range(-1_000_000, 1_000_000)]
    public int Points { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public class UpdateCustomerRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
}
