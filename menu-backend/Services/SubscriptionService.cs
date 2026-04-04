using System.Security.Cryptography;
using System.Text;
using menu_backend.Data;
using menu_backend.DTOs.Subscription;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace menu_backend.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly List<string> StandardFeatures = new()
    {
        "Menu Management", "Order Management", "Table Management",
        "Dashboard & Analytics", "Bills & Dues", "Staff Management",
        "Website Builder", "QR Codes", "Kitchen Display", "Waiter App"
    };

    private static readonly List<string> PremiumFeatures = new()
    {
        "Menu Management", "Order Management", "Table Management",
        "Dashboard & Analytics", "Bills & Dues", "Staff Management",
        "Website Builder", "QR Codes", "Kitchen Display", "Waiter App",
        "AI Marketing", "Google Reviews", "Inventory Management"
    };

    public SubscriptionService(
        AppDbContext db,
        ITenantProvider tenantProvider,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SubscriptionStatusResponse> GetStatusAsync()
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context required");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found");

        var trialEndsAt = tenant.CreatedAt.AddDays(30);
        var now = DateTime.UtcNow;
        var isTrialActive = now < trialEndsAt;
        var trialDaysRemaining = isTrialActive ? (int)Math.Ceiling((trialEndsAt - now).TotalDays) : 0;

        // Find latest stored subscription mapping
        var mapping = await _db.Set<TenantSubscription>()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (mapping != null)
        {
            // Fetch live status from Razorpay
            var rzSub = await FetchRazorpaySubscriptionAsync(mapping.RazorpaySubscriptionId);
            if (rzSub is JsonElement sub)
            {
                var rzStatus = sub.GetProperty("status").GetString(); // created, authenticated, active, pending, halted, cancelled, completed, expired
                var isActive = rzStatus == "active" || rzStatus == "authenticated";

                if (isActive)
                {
                    var planId = sub.GetProperty("plan_id").GetString() ?? "";
                    var (plan, cycle) = ResolvePlanFromId(planId);
                    var features = plan == "Premium" ? PremiumFeatures : StandardFeatures;

                    DateTime? expiresAt = null;
                    if (sub.TryGetProperty("current_end", out var endProp) && endProp.ValueKind == JsonValueKind.Number)
                        expiresAt = DateTimeOffset.FromUnixTimeSeconds(endProp.GetInt64()).UtcDateTime;

                    return new SubscriptionStatusResponse
                    {
                        IsTrialActive = false,
                        IsSubscriptionActive = true,
                        RequiresSubscription = false,
                        TrialDaysRemaining = 0,
                        Plan = plan,
                        Cycle = cycle,
                        Status = "Active",
                        ExpiresAt = expiresAt,
                        AvailableFeatures = features
                    };
                }
            }
        }

        // No active Razorpay subscription found
        if (isTrialActive)
        {
            return new SubscriptionStatusResponse
            {
                IsTrialActive = true,
                IsSubscriptionActive = false,
                RequiresSubscription = false,
                TrialDaysRemaining = trialDaysRemaining,
                Plan = "Premium", // Full access during trial
                Status = "Trial",
                AvailableFeatures = PremiumFeatures
            };
        }

        // Trial expired, no subscription
        return new SubscriptionStatusResponse
        {
            IsTrialActive = false,
            IsSubscriptionActive = false,
            RequiresSubscription = true,
            TrialDaysRemaining = 0,
            Status = "Expired",
            AvailableFeatures = new List<string>()
        };
    }

    public Task<List<SubscriptionPlanDto>> GetPlansAsync()
    {
        var plans = new List<SubscriptionPlanDto>
        {
            new() { Plan = "Standard", Cycle = "Monthly", Price = 999m,
                     DisplayName = "Standard Monthly", Features = StandardFeatures },
            new() { Plan = "Standard", Cycle = "Yearly", Price = 9999m,
                     DisplayName = "Standard Yearly", Features = StandardFeatures },
            new() { Plan = "Premium", Cycle = "Monthly", Price = 1999m,
                     DisplayName = "Premium Monthly", Features = PremiumFeatures },
            new() { Plan = "Premium", Cycle = "Yearly", Price = 19999m,
                     DisplayName = "Premium Yearly", Features = PremiumFeatures },
        };

        return Task.FromResult(plans);
    }

    public async Task<CreateRazorpaySubscriptionResponse> CreateSubscriptionAsync(CreateRazorpaySubscriptionRequest request)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context required");

        var planKey = $"{request.Plan}{request.Cycle}"; // e.g. "StandardMonthly"
        var razorpayPlanId = _config[$"Razorpay:Plans:{planKey}"]
            ?? throw new ArgumentException($"Razorpay plan ID not configured for {planKey}");

        var keyId = _config["Razorpay:KeyId"]
            ?? throw new InvalidOperationException("Razorpay KeyId not configured");

        var client = CreateRazorpayClient();

        var payload = new
        {
            plan_id = razorpayPlanId,
            total_count = 12,
            quantity = 1,
            notes = new { tenant_id = tenantId }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("https://api.razorpay.com/v1/subscriptions", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var subResponse = JsonSerializer.Deserialize<JsonElement>(body);
        var subscriptionId = subResponse.GetProperty("id").GetString()!;

        return new CreateRazorpaySubscriptionResponse
        {
            SubscriptionId = subscriptionId,
            RazorpayKeyId = keyId
        };
    }

    public async Task<SubscriptionStatusResponse> VerifyPaymentAsync(VerifyPaymentRequest request)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context required");

        // Verify signature: razorpay_payment_id | razorpay_subscription_id
        var keySecret = _config["Razorpay:KeySecret"]
            ?? throw new InvalidOperationException("Razorpay KeySecret not configured");

        var payload = $"{request.RazorpayPaymentId}|{request.RazorpaySubscriptionId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "").ToLower();

        if (computedHash != request.RazorpaySignature)
            throw new InvalidOperationException("Payment verification failed: invalid signature");

        // Store the tenant ↔ subscription mapping (upsert)
        var existing = await _db.Set<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.RazorpaySubscriptionId == request.RazorpaySubscriptionId);

        if (existing == null)
        {
            _db.Set<TenantSubscription>().Add(new TenantSubscription
            {
                TenantId = tenantId,
                RazorpaySubscriptionId = request.RazorpaySubscriptionId
            });
            await _db.SaveChangesAsync();
        }

        return await GetStatusAsync();
    }

    // ── Helpers ──

    private HttpClient CreateRazorpayClient()
    {
        var keyId = _config["Razorpay:KeyId"]!;
        var keySecret = _config["Razorpay:KeySecret"]!;
        var client = _httpClientFactory.CreateClient();
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        return client;
    }

    private async Task<JsonElement?> FetchRazorpaySubscriptionAsync(string subscriptionId)
    {
        try
        {
            var client = CreateRazorpayClient();
            var response = await client.GetAsync($"https://api.razorpay.com/v1/subscriptions/{subscriptionId}");
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a Razorpay plan_id back to (Plan, Cycle) using appsettings config.
    /// </summary>
    private (string Plan, string Cycle) ResolvePlanFromId(string razorpayPlanId)
    {
        var plans = _config.GetSection("Razorpay:Plans");
        foreach (var child in plans.GetChildren())
        {
            if (child.Value == razorpayPlanId)
            {
                var key = child.Key; // e.g. "StandardMonthly", "PremiumYearly"
                if (key.StartsWith("Premium"))
                    return ("Premium", key.Replace("Premium", ""));
                return ("Standard", key.Replace("Standard", ""));
            }
        }
        // Fallback — treat unknown plans as Standard Monthly
        return ("Standard", "Monthly");
    }
}
