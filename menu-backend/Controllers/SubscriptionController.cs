using menu_backend.DTOs;
using menu_backend.DTOs.Subscription;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _subscriptionService.GetStatusAsync();
        return Ok(ApiResponse<SubscriptionStatusResponse>.Ok(status));
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.GetPlansAsync();
        return Ok(ApiResponse<List<SubscriptionPlanDto>>.Ok(plans));
    }

    [HttpPost("create-subscription")]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateRazorpaySubscriptionRequest request)
    {
        var sub = await _subscriptionService.CreateSubscriptionAsync(request);
        return Ok(ApiResponse<CreateRazorpaySubscriptionResponse>.Ok(sub));
    }

    [HttpPost("verify-payment")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        var status = await _subscriptionService.VerifyPaymentAsync(request);
        return Ok(ApiResponse<SubscriptionStatusResponse>.Ok(status, "Subscription activated successfully!"));
    }
}
