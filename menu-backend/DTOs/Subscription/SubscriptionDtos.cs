using System.ComponentModel.DataAnnotations;

namespace menu_backend.DTOs.Subscription;

public class SubscriptionStatusResponse
{
    public bool IsTrialActive { get; set; }
    public bool IsSubscriptionActive { get; set; }
    public bool RequiresSubscription { get; set; }
    public int TrialDaysRemaining { get; set; }
    public string? Plan { get; set; }
    public string? Cycle { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string> AvailableFeatures { get; set; } = new();
}

public class SubscriptionPlanDto
{
    public string Plan { get; set; } = string.Empty;
    public string Cycle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
}

public class CreateRazorpaySubscriptionRequest
{
    [Required]
    public string Plan { get; set; } = string.Empty; // Standard, Premium

    [Required]
    public string Cycle { get; set; } = string.Empty; // Monthly, Yearly
}

public class CreateRazorpaySubscriptionResponse
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string RazorpayKeyId { get; set; } = string.Empty;
}

public class VerifyPaymentRequest
{
    [Required]
    public string RazorpaySubscriptionId { get; set; } = string.Empty;

    [Required]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    [Required]
    public string RazorpaySignature { get; set; } = string.Empty;
}
