using menu_backend.Data;
using menu_backend.DTOs;
using menu_backend.DTOs.Hotel;
using menu_backend.DTOs.Order;
using menu_backend.Helpers;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace menu_backend.Controllers;

/// <summary>Hotel: rooms, guest stays (check-in/out, room bills) and the room service delivery queue.</summary>
[ApiController]
[Route("api/[controller]")]
// Roles are set per endpoint (a class-level role list would also apply to, and block, wider endpoints)
[Authorize]
public class RoomController : ControllerBase
{
    private readonly IHotelService _hotel;
    private readonly IOrderService _orders;

    public RoomController(IHotelService hotel, IOrderService orders)
    {
        _hotel = hotel;
        _orders = orders;
    }

    [HttpGet]
    [Authorize(Roles = Roles.FrontDesk)]
    public async Task<IActionResult> GetRooms() =>
        Ok(ApiResponse<List<RoomResponse>>.Ok(await _hotel.GetRoomsAsync()));

    [HttpPost]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> CreateRoom([FromBody] RoomRequest request) =>
        Run(async () => ApiResponse<RoomResponse>.Ok(await _hotel.CreateRoomAsync(request), "Room added."));

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> UpdateRoom(Guid id, [FromBody] RoomRequest request) =>
        Run(async () => ApiResponse<RoomResponse>.Ok(await _hotel.UpdateRoomAsync(id, request)));

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Management)]
    public Task<IActionResult> DeleteRoom(Guid id) =>
        Run(async () => { await _hotel.DeleteRoomAsync(id); return ApiResponse.Ok("Room removed."); });

    [HttpGet("{id}/qr")]
    [Authorize(Roles = Roles.FrontDesk)]
    public IActionResult GetRoomQr(Guid id, [FromServices] ITenantProvider tenantProvider) =>
        File(_hotel.BuildRoomQr(id, tenantProvider.TenantId!), "image/png", $"room-qr-{id}.png");

    [HttpPost("{id}/check-in")]
    [Authorize(Roles = Roles.FrontDesk)]
    public Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request) =>
        Run(async () => ApiResponse<StayResponse>.Ok(await _hotel.CheckInAsync(id, request), "Guest checked in."));

    [HttpGet("stays/{stayId}/folio")]
    [Authorize(Roles = Roles.FrontDesk)]
    public Task<IActionResult> GetFolio(Guid stayId) =>
        Run(async () => ApiResponse<FolioResponse>.Ok(await _hotel.GetFolioAsync(stayId)));

    [HttpPost("stays/{stayId}/check-out")]
    [Authorize(Roles = Roles.FrontDesk)]
    public Task<IActionResult> CheckOut(Guid stayId, [FromBody] ClearTableRequest request) =>
        Run(async () => ApiResponse<CheckOutResponse>.Ok(await _hotel.CheckOutAsync(stayId, request), "Guest checked out."));

    /// <summary>Room QR page: room number and whether it can order. No guest details.</summary>
    [HttpGet("public/{id}")]
    [AllowAnonymous]
    public Task<IActionResult> GetPublicRoom(Guid id) =>
        Run(async () => ApiResponse<PublicRoomResponse>.Ok(await _hotel.GetPublicRoomAsync(id)));

    // ── Room service delivery ──

    [HttpGet("service-queue")]
    [Authorize(Roles = Roles.RoomServiceStaff)]
    public async Task<IActionResult> GetServiceQueue() =>
        Ok(ApiResponse<List<OrderResponse>>.Ok(await _orders.GetRoomServiceQueueAsync()));

    [HttpPost("orders/{orderId}/dispatch")]
    [Authorize(Roles = Roles.RoomServiceStaff)]
    public Task<IActionResult> Dispatch(Guid orderId) =>
        Run(async () => ApiResponse<OrderResponse>.Ok(await _orders.DispatchRoomOrderAsync(orderId)));

    [HttpPost("orders/{orderId}/deliver")]
    [Authorize(Roles = Roles.RoomServiceStaff)]
    public Task<IActionResult> Deliver(Guid orderId) =>
        Run(async () => ApiResponse<OrderResponse>.Ok(await _orders.DeliverRoomOrderAsync(orderId)));

    /// <summary>Attendant collected payment on delivery: bill the order.</summary>
    [HttpPost("orders/settle")]
    [Authorize(Roles = Roles.RoomServiceStaff)]
    public Task<IActionResult> SettleOnDelivery([FromBody] ClearTableRequest request) =>
        Run(async () => ApiResponse<TableSessionSummary>.Ok(await _orders.SettleOrdersAsync(request), "Payment recorded."));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
