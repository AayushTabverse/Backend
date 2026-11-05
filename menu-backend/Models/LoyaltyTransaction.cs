using System.ComponentModel.DataAnnotations;

namespace menu_backend.Models;

public enum LoyaltyTransactionType
{
    Earn = 0,
    Redeem = 1,
    Expire = 2,
    /// <summary>Welcome or birthday bonus.</summary>
    Bonus = 3,
    /// <summary>Changed by hand by the owner/manager.</summary>
    Adjust = 4,
    /// <summary>Points taken back because a bill was reduced after printing.</summary>
    Reverse = 5
}

/// <summary>One change to a customer's loyalty points. The balance is the sum of these.</summary>
public class LoyaltyTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }

    public LoyaltyTransactionType Type { get; set; }

    /// <summary>Positive adds points, negative takes them away.</summary>
    public int Points { get; set; }

    public int BalanceAfter { get; set; }

    [MaxLength(20)]
    public string? BillNumber { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    /// <summary>Staff member who settled the bill or made the change.</summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    public Customer? Customer { get; set; }
}
