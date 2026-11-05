using menu_backend.DTOs.Hotel;
using menu_backend.DTOs.Order;

namespace menu_backend.Services.Interfaces;

/// <summary>Hotel rooms and guest stays (check-in, room bill, checkout). No room bookings.</summary>
public interface IHotelService
{
    Task<List<RoomResponse>> GetRoomsAsync();
    Task<RoomResponse> CreateRoomAsync(RoomRequest request);
    Task<RoomResponse> UpdateRoomAsync(Guid id, RoomRequest request);
    Task DeleteRoomAsync(Guid id);

    Task<StayResponse> CheckInAsync(Guid roomId, CheckInRequest request);
    Task<FolioResponse> GetFolioAsync(Guid stayId);
    Task<CheckOutResponse> CheckOutAsync(Guid stayId, ClearTableRequest request);

    Task<PublicRoomResponse> GetPublicRoomAsync(Guid roomId);
    byte[] BuildRoomQr(Guid roomId, string tenantId);
}
