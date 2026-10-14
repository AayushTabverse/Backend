using menu_backend.DTOs.Waitlist;

namespace menu_backend.Services.Interfaces;

public interface IWaitlistService
{
    Task<List<WaitlistEntryResponse>> GetEntriesAsync(bool includeClosedToday);
    Task<WaitlistEntryResponse> AddEntryAsync(CreateWaitlistEntryRequest request);
    Task<WaitlistEntryResponse> UpdateEntryAsync(Guid id, UpdateWaitlistEntryRequest request);
    Task<WaitlistEntryResponse> NotifyAsync(Guid id);
    Task<WaitlistEntryResponse> SeatAsync(Guid id, SeatWaitlistEntryRequest request);
    Task<WaitlistEntryResponse> ReleaseAsync(Guid id);

    /// <summary>Seats an arrived reservation party: creates a seated entry that holds the tables. Returns its id.</summary>
    Task<Guid> SeatReservationAsync(Models.Reservation reservation, List<Guid> tableIds, bool force);
    Task<WaitlistEntryResponse> CancelAsync(Guid id);
    Task<WaitlistEntryResponse> MarkNoShowAsync(Guid id);
    Task<WaitlistStatsResponse> GetStatsAsync();

    Task<WaitlistSettingsResponse> GetSettingsAsync();
    Task<WaitlistSettingsResponse> UpdateSettingsAsync(UpdateWaitlistSettingsRequest request);
    Task<WaitlistAnalyticsResponse> GetAnalyticsAsync(DateTime from, DateTime to);

    // Public (customer-facing)
    Task<PublicWaitlistInfoResponse> GetPublicInfoAsync(string tenantId);
    Task<PublicWaitlistStatusResponse> JoinAsync(string tenantId, PublicJoinWaitlistRequest request);
    Task<PublicWaitlistStatusResponse> GetPublicStatusAsync(Guid id);
    Task<PublicWaitlistStatusResponse> LeaveAsync(Guid id);

    /// <summary>Marks called parties who never arrived as no-shows, per each restaurant's timeout. Returns how many.</summary>
    Task<int> ExpireNoShowsAsync();

    /// <summary>Called when a table is cleared: completes the seated party holding it and frees its merged tables.</summary>
    Task ReleaseTableAsync(Guid tableId);
}
