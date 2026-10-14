using menu_backend.DTOs.Reservation;

namespace menu_backend.Services.Interfaces;

public interface IReservationService
{
    /// <summary>Bookings on restaurant-local days from..to, plus every pending request.</summary>
    Task<List<ReservationResponse>> GetReservationsAsync(DateTime from, DateTime to);
    Task<int> GetPendingCountAsync();
    Task<ReservationResponse> CreateAsync(CreateReservationRequest request);
    Task<ReservationResponse> UpdateAsync(Guid id, UpdateReservationRequest request);
    Task<ReservationResponse> AcceptAsync(Guid id, AcceptReservationRequest request);
    Task<ReservationResponse> RejectAsync(Guid id, RejectReservationRequest request);
    Task<ReservationResponse> CancelAsync(Guid id);
    Task<ReservationResponse> MarkNoShowAsync(Guid id);
    Task<ReservationResponse> SeatAsync(Guid id, SeatReservationRequest request);

    Task<ReservationSettingsResponse> GetSettingsAsync();
    Task<ReservationSettingsResponse> UpdateSettingsAsync(UpdateReservationSettingsRequest request);

    // Public (restaurant website)
    Task<PublicReservationInfoResponse> GetPublicInfoAsync(string tenantId);
    Task<PublicReservationStatusResponse> RequestAsync(string tenantId, PublicReservationRequest request);
    Task<PublicReservationStatusResponse> GetPublicStatusAsync(Guid id);
    Task<PublicReservationStatusResponse> CancelByCustomerAsync(Guid id);

    /// <summary>Confirmed bookings past their grace period → no-show; unanswered requests past their time → rejected.</summary>
    Task<int> ExpireAsync();
}
