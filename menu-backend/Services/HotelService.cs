using System.Security.Cryptography;
using menu_backend.Data;
using menu_backend.DTOs.Hotel;
using menu_backend.DTOs.Order;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace menu_backend.Services;

public class HotelService : IHotelService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOrderService _orderService;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IConfiguration _config;

    public HotelService(AppDbContext db, ITenantProvider tenantProvider, IOrderService orderService,
        IHubContext<OrderHub> hub, IConfiguration config)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _orderService = orderService;
        _hub = hub;
        _config = config;
    }

    public async Task<List<RoomResponse>> GetRoomsAsync()
    {
        var rooms = await _db.Rooms.OrderBy(r => r.Floor).ThenBy(r => r.RoomNumber).ToListAsync();
        var stays = await _db.Stays.Where(s => s.Status == StayStatus.Active).ToListAsync();
        var open = await _db.Orders
            .Where(o => o.StayId != null && o.BillNumber == null && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.StayId, o.TotalAmount })
            .ToListAsync();

        return rooms.Select(r =>
        {
            var stay = stays.Where(s => s.RoomId == r.Id).OrderByDescending(s => s.CheckInAt).FirstOrDefault();
            var owed = stay == null ? new() : open.Where(o => o.StayId == stay.Id).ToList();
            return new RoomResponse
            {
                Id = r.Id,
                RoomNumber = r.RoomNumber,
                Floor = r.Floor,
                RoomType = r.RoomType,
                IsActive = r.IsActive,
                Stay = stay == null ? null : MapStay(stay, r),
                OpenBalance = owed.Sum(o => o.TotalAmount),
                OpenOrderCount = owed.Count
            };
        }).ToList();
    }

    public async Task<RoomResponse> CreateRoomAsync(RoomRequest request)
    {
        var number = request.RoomNumber.Trim();
        if (await _db.Rooms.AnyAsync(r => r.RoomNumber == number))
            throw new InvalidOperationException($"Room {number} already exists.");

        var room = new Room
        {
            TenantId = _tenantProvider.TenantId!,
            RoomNumber = number,
            Floor = Clean(request.Floor),
            RoomType = Clean(request.RoomType),
            IsActive = request.IsActive
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return new RoomResponse { Id = room.Id, RoomNumber = room.RoomNumber, Floor = room.Floor, RoomType = room.RoomType, IsActive = room.IsActive };
    }

    public async Task<RoomResponse> UpdateRoomAsync(Guid id, RoomRequest request)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Room not found.");
        var number = request.RoomNumber.Trim();
        if (number != room.RoomNumber && await _db.Rooms.AnyAsync(r => r.RoomNumber == number))
            throw new InvalidOperationException($"Room {number} already exists.");

        room.RoomNumber = number;
        room.Floor = Clean(request.Floor);
        room.RoomType = Clean(request.RoomType);
        room.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return (await GetRoomsAsync()).First(r => r.Id == id);
    }

    public async Task DeleteRoomAsync(Guid id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Room not found.");
        if (await _db.Stays.AnyAsync(s => s.RoomId == id && s.Status == StayStatus.Active))
            throw new InvalidOperationException($"Room {room.RoomNumber} has a guest checked in. Check them out first.");

        room.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<StayResponse> CheckInAsync(Guid roomId, CheckInRequest request)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId)
            ?? throw new KeyNotFoundException("Room not found.");
        if (!room.IsActive)
            throw new InvalidOperationException($"Room {room.RoomNumber} is switched off.");
        if (await _db.Stays.AnyAsync(s => s.RoomId == roomId && s.Status == StayStatus.Active))
            throw new InvalidOperationException($"Room {room.RoomNumber} already has a guest. Check them out first.");

        var stay = new Stay
        {
            TenantId = room.TenantId,
            RoomId = room.Id,
            GuestName = request.GuestName.Trim(),
            GuestMobile = Clean(request.GuestMobile),
            ExpectedCheckOutAt = request.ExpectedCheckOutAt,
            Notes = Clean(request.Notes),
            Pin = request.RequirePin ? RandomNumberGenerator.GetInt32(1000, 10000).ToString() : null
        };
        _db.Stays.Add(stay);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(room.TenantId).SendAsync("RoomsChanged");
        return MapStay(stay, room);
    }

    public async Task<FolioResponse> GetFolioAsync(Guid stayId)
    {
        var stay = await _db.Stays.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == stayId)
            ?? throw new KeyNotFoundException("Stay not found.");
        var orders = await _orderService.GetStayOrdersAsync(stayId);
        return new FolioResponse
        {
            Stay = MapStay(stay, stay.Room),
            Orders = orders,
            OpenBalance = orders.Where(o => o.BillNumber == null).Sum(o => o.TotalAmount)
        };
    }

    public async Task<CheckOutResponse> CheckOutAsync(Guid stayId, ClearTableRequest request)
    {
        var stay = await _db.Stays.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == stayId)
            ?? throw new KeyNotFoundException("Stay not found.");
        if (stay.Status == StayStatus.CheckedOut)
            throw new InvalidOperationException("This guest has already checked out.");

        request.CustomerName ??= stay.GuestName;
        request.CustomerMobile ??= stay.GuestMobile;
        var bill = await _orderService.SettleStayAsync(stayId, request);

        stay.Status = StayStatus.CheckedOut;
        stay.CheckedOutAt = DateTime.UtcNow;
        stay.BillNumber = bill?.Orders.FirstOrDefault()?.BillNumber;
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(stay.TenantId).SendAsync("RoomsChanged");

        return new CheckOutResponse { Stay = MapStay(stay, stay.Room), Bill = bill };
    }

    public async Task<PublicRoomResponse> GetPublicRoomAsync(Guid roomId)
    {
        var room = await _db.Rooms.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted && r.IsActive)
            ?? throw new KeyNotFoundException("Room not found.");
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == room.TenantId);
        var stay = await _db.Stays.IgnoreQueryFilters()
            .Where(s => s.RoomId == roomId && !s.IsDeleted && s.Status == StayStatus.Active)
            .OrderByDescending(s => s.CheckInAt)
            .FirstOrDefaultAsync();

        return new PublicRoomResponse
        {
            RoomId = room.Id,
            TenantId = room.TenantId,
            RoomNumber = room.RoomNumber,
            CheckedIn = stay != null,
            PinRequired = !string.IsNullOrEmpty(stay?.Pin),
            GuestFirstName = stay?.GuestName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            RoomServiceEnabled = tenant?.HotelModeEnabled == true
        };
    }

    public byte[] BuildRoomQr(Guid roomId, string tenantId)
    {
        var baseUrl = _config["App:BaseUrl"] ?? "https://yourapp.com";
        var url = $"{baseUrl}/menu/{tenantId}/room-{roomId}";
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(20);
    }

    private static StayResponse MapStay(Stay s, Room? room) => new()
    {
        Id = s.Id,
        RoomId = s.RoomId,
        RoomNumber = room?.RoomNumber ?? "",
        GuestName = s.GuestName,
        GuestMobile = s.GuestMobile,
        Pin = s.Pin,
        Status = s.Status.ToString(),
        CheckInAt = s.CheckInAt,
        ExpectedCheckOutAt = s.ExpectedCheckOutAt,
        CheckedOutAt = s.CheckedOutAt,
        BillNumber = s.BillNumber,
        Notes = s.Notes
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
