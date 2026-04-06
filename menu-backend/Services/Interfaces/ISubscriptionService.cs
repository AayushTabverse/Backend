using menu_backend.DTOs.Subscription;

namespace menu_backend.Services.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionStatusResponse> GetStatusAsync();
    Task<List<SubscriptionPlanDto>> GetPlansAsync();
    Task<CreateRazorpaySubscriptionResponse> CreateSubscriptionAsync(CreateRazorpaySubscriptionRequest request);
    Task<SubscriptionStatusResponse> VerifyPaymentAsync(VerifyPaymentRequest request);
    Task<SubscriptionStatusResponse> CancelSubscriptionAsync(CancelSubscriptionRequest request);
    Task<UpdateSubscriptionResponse> UpdateSubscriptionAsync(UpdateSubscriptionRequest request);
}
