using menu_backend.Data;
using menu_backend.DTOs.Reservation;
using menu_backend.Helpers;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

public class ReservationService : IReservationService
{
    /// <summary>Website requests must be at least this far ahead…</summary>
    private static readonly TimeSpan MinLeadTime = TimeSpan.FromMinutes(30);
    /// <summary>…and at most this far ahead.</summary>
    private static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(60);

    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IWaitlistService _waitlistService;

    public ReservationService(AppDbContext db, ITenantProvider tenantProvider, IHubContext<OrderHub> hub, IWaitlistService waitlistService)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _hub = hub;
        _waitlistService = waitlistService;
    }

    public async Task<List<ReservationResponse>> GetReservationsAsync(DateTime from, DateTime to)
    {
        var (start, end) = BusinessClock.DayRangeUtc(from, to);

        var reservations = await Query()
            .Where(r => r.Status == ReservationStatus.Pending || (r.ReservedFor >= start && r.ReservedFor < end))
            .OrderBy(r => r.ReservedFor)
            .ToListAsync();

        return reservations.Select(Map).ToList();
    }

    public Task<int> GetPendingCountAsync() =>
        _db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending);

    public async Task<ReservationResponse> CreateAsync(CreateReservationRequest request)
    {
        if (request.ReservedFor.ToUniversalTime() < DateTime.UtcNow.AddHours(-2))
            throw new InvalidOperationException("The booking time is in the past.");

        var reservation = new Reservation
        {
            TenantId = _tenantProvider.TenantId!,
            CustomerName = request.CustomerName.Trim(),
            CustomerMobile = NormalizeMobile(request.CustomerMobile),
            CustomerEmail = Clean(request.CustomerEmail),
            PartySize = request.PartySize,
            ReservedFor = request.ReservedFor.ToUniversalTime(),
            DurationMinutes = request.DurationMinutes,
            Notes = Clean(request.Notes),
            Status = ReservationStatus.Confirmed,
            Source = ReservationSource.Staff,
            RespondedAt = DateTime.UtcNow
        };
        ValidateMobile(reservation.CustomerMobile);

        if (request.TableIds is { Count: > 0 })
            await SetTablesAsync(reservation, request.TableIds);

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(await LoadAsync(reservation.Id));
    }

    public async Task<ReservationResponse> UpdateAsync(Guid id, UpdateReservationRequest request)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            throw new InvalidOperationException("Only upcoming bookings can be edited.");

        if (request.CustomerName != null) reservation.CustomerName = request.CustomerName.Trim();
        if (request.CustomerMobile != null)
        {
            reservation.CustomerMobile = NormalizeMobile(request.CustomerMobile);
            ValidateMobile(reservation.CustomerMobile);
        }
        if (request.CustomerEmail != null) reservation.CustomerEmail = Clean(request.CustomerEmail);
        if (request.PartySize.HasValue) reservation.PartySize = request.PartySize.Value;
        if (request.ReservedFor.HasValue) reservation.ReservedFor = request.ReservedFor.Value.ToUniversalTime();
        if (request.DurationMinutes.HasValue) reservation.DurationMinutes = request.DurationMinutes.Value;
        if (request.Notes != null) reservation.Notes = Clean(request.Notes);

        // Time or length changed → re-check the current tables; a new table list replaces them
        var tableIds = request.TableIds ?? reservation.Tables.Where(t => !t.IsDeleted).Select(t => t.TableId).ToList();
        await SetTablesAsync(reservation, tableIds);

        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(await LoadAsync(id));
    }

    public async Task<ReservationResponse> AcceptAsync(Guid id, AcceptReservationRequest request)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status != ReservationStatus.Pending)
            throw new InvalidOperationException("This request has already been answered.");

        if (request.TableIds is { Count: > 0 })
            await SetTablesAsync(reservation, request.TableIds);

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ResponseMessage = Clean(request.Message);
        reservation.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(await LoadAsync(id));
    }

    public async Task<ReservationResponse> RejectAsync(Guid id, RejectReservationRequest request)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status != ReservationStatus.Pending)
            throw new InvalidOperationException("This request has already been answered.");

        reservation.Status = ReservationStatus.Rejected;
        reservation.ResponseMessage = Clean(request.Message);
        reservation.RespondedAt = DateTime.UtcNow;
        reservation.ClosedAt = DateTime.UtcNow;
        ReleaseTables(reservation);

        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(reservation);
    }

    public async Task<ReservationResponse> CancelAsync(Guid id)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            throw new InvalidOperationException("Only upcoming bookings can be cancelled.");

        Close(reservation, ReservationStatus.Cancelled);
        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(reservation);
    }

    public async Task<ReservationResponse> MarkNoShowAsync(Guid id)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed bookings can be marked as no-show.");

        Close(reservation, ReservationStatus.NoShow);
        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(reservation);
    }

    public async Task<ReservationResponse> SeatAsync(Guid id, SeatReservationRequest request)
    {
        var reservation = await LoadAsync(id);
        if (reservation.Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException(reservation.Status == ReservationStatus.Pending
                ? "Accept this request before seating the party."
                : "This booking can't be seated.");

        // Hold the tables through a seated waitlist entry (same as walk-ins): released when the bill is cleared
        var entryId = await _waitlistService.SeatReservationAsync(reservation, request.TableIds, request.Force);

        // Keep the booking's tables in line with where the party actually sat
        foreach (var link in reservation.Tables.Where(t => !t.IsDeleted)) link.IsDeleted = true;
        foreach (var tableId in request.TableIds.Distinct())
            _db.ReservationTables.Add(new ReservationTable { TenantId = reservation.TenantId, ReservationId = reservation.Id, TableId = tableId });

        reservation.Status = ReservationStatus.Seated;
        reservation.SeatedAt = DateTime.UtcNow;
        reservation.WaitlistEntryId = entryId;

        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return Map(await LoadAsync(id));
    }

    // ── Settings ──

    public async Task<ReservationSettingsResponse> GetSettingsAsync() =>
        MapSettings(await LoadTenantAsync(_tenantProvider.TenantId!));

    public async Task<ReservationSettingsResponse> UpdateSettingsAsync(UpdateReservationSettingsRequest request)
    {
        var tenant = await LoadTenantAsync(_tenantProvider.TenantId!);
        if (request.OnlineReservationsEnabled.HasValue) tenant.OnlineReservationsEnabled = request.OnlineReservationsEnabled.Value;
        if (request.GraceMinutes.HasValue) tenant.ReservationGraceMinutes = request.GraceMinutes.Value;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapSettings(tenant);
    }

    // ── Public (restaurant website) ──
    // No tenant claim on these requests, so every query is scoped to the tenant explicitly.

    public async Task<PublicReservationInfoResponse> GetPublicInfoAsync(string tenantId)
    {
        var tenant = await LoadTenantAsync(tenantId);
        return new PublicReservationInfoResponse
        {
            RestaurantName = tenant.Name,
            LogoUrl = tenant.LogoUrl,
            OnlineReservationsEnabled = tenant.OnlineReservationsEnabled,
            Phone = tenant.Phone
        };
    }

    public async Task<PublicReservationStatusResponse> RequestAsync(string tenantId, PublicReservationRequest request)
    {
        var tenant = await LoadTenantAsync(tenantId);
        if (!tenant.OnlineReservationsEnabled)
            throw new InvalidOperationException("Online booking isn't available. Please call the restaurant.");

        var mobile = NormalizeMobile(request.CustomerMobile);
        ValidateMobile(mobile);

        var when = request.ReservedFor.ToUniversalTime();
        var now = DateTime.UtcNow;
        if (when < now + MinLeadTime)
            throw new InvalidOperationException("Please pick a time at least 30 minutes from now.");
        if (when > now + MaxLeadTime)
            throw new InvalidOperationException("Bookings open up to 60 days ahead.");

        // Same phone already has a booking that day → show that one instead of a duplicate
        var day = DateOnly.FromDateTime(BusinessClock.ToLocal(when));
        var (dayStart, dayEnd) = BusinessClock.DayRangeUtc(day.ToDateTime(TimeOnly.MinValue), day.ToDateTime(TimeOnly.MinValue));
        var existing = await _db.Reservations
            .Where(r => r.TenantId == tenantId && r.CustomerMobile == mobile
                     && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                     && r.ReservedFor >= dayStart && r.ReservedFor < dayEnd)
            .FirstOrDefaultAsync();
        if (existing != null)
            return BuildPublicStatus(existing, tenant);

        var reservation = new Reservation
        {
            TenantId = tenantId,
            CustomerName = request.CustomerName.Trim(),
            CustomerMobile = mobile,
            CustomerEmail = Clean(request.CustomerEmail),
            PartySize = request.PartySize,
            ReservedFor = when,
            Notes = Clean(request.Notes),
            Status = ReservationStatus.Pending,
            Source = ReservationSource.Website
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);
        await _hub.Clients.Group(tenantId).SendAsync("ReservationRequested", new
        {
            reservation.Id,
            reservation.CustomerName,
            reservation.PartySize,
            reservation.ReservedFor
        });

        return BuildPublicStatus(reservation, tenant);
    }

    public async Task<PublicReservationStatusResponse> GetPublicStatusAsync(Guid id)
    {
        var reservation = await LoadAsync(id);
        return BuildPublicStatus(reservation, await LoadTenantAsync(reservation.TenantId));
    }

    public async Task<PublicReservationStatusResponse> CancelByCustomerAsync(Guid id)
    {
        var reservation = await LoadAsync(id);
        if (!CanCustomerCancel(reservation))
            throw new InvalidOperationException("This booking can no longer be cancelled online. Please call the restaurant.");

        Close(reservation, ReservationStatus.Cancelled);
        reservation.Notes = string.IsNullOrEmpty(reservation.Notes)
            ? "Cancelled by customer"
            : $"{reservation.Notes} · Cancelled by customer";
        await _db.SaveChangesAsync();
        await BroadcastAsync(reservation);

        return BuildPublicStatus(reservation, await LoadTenantAsync(reservation.TenantId));
    }

    // ── Expiry (background) ──

    public async Task<int> ExpireAsync()
    {
        var now = DateTime.UtcNow;
        var graceByTenant = await _db.Tenants
            .Select(t => new { t.TenantId, t.ReservationGraceMinutes })
            .ToDictionaryAsync(t => t.TenantId, t => t.ReservationGraceMinutes);

        var candidates = await _db.Reservations
            .IgnoreQueryFilters()
            .Include(r => r.Tables)
            .Where(r => !r.IsDeleted && r.ReservedFor < now
                     && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .ToListAsync();

        var changed = new List<Reservation>();
        foreach (var r in candidates)
        {
            if (r.Status == ReservationStatus.Pending)
            {
                // Nobody answered before the booking time
                r.Status = ReservationStatus.Rejected;
                r.ResponseMessage ??= "Sorry, we couldn't confirm your booking in time.";
                r.ClosedAt = now;
                ReleaseTables(r);
                changed.Add(r);
            }
            else if (graceByTenant.TryGetValue(r.TenantId, out var grace) && grace > 0 && r.ReservedFor.AddMinutes(grace) <= now)
            {
                Close(r, ReservationStatus.NoShow);
                r.Notes = string.IsNullOrEmpty(r.Notes) ? "Auto no-show" : $"{r.Notes} · Auto no-show";
                changed.Add(r);
            }
        }
        if (changed.Count == 0) return 0;

        await _db.SaveChangesAsync();
        foreach (var r in changed)
            await BroadcastAsync(r);
        return changed.Count;
    }

    // ── Helpers ──

    /// <summary>
    /// Assigns tables to a booking after checking they exist, are usable and aren't booked by
    /// another confirmed reservation in an overlapping time window.
    /// </summary>
    private async Task SetTablesAsync(Reservation reservation, List<Guid> requestedIds)
    {
        var tableIds = requestedIds.Distinct().ToList();

        // A restaurant has few tables; filter in memory (the MySQL provider can't translate Guid-list Contains)
        var tables = (await _db.Tables.ToListAsync()).Where(t => tableIds.Contains(t.Id)).ToList();
        if (tables.Count != tableIds.Count)
            throw new KeyNotFoundException("One or more tables were not found.");

        var inactive = tables.Where(t => !t.IsActive).Select(t => t.TableNumber).ToList();
        if (inactive.Count > 0)
            throw new InvalidOperationException($"Table {string.Join(", ", inactive)} is inactive.");

        if (tableIds.Count > 0)
        {
            var start = reservation.ReservedFor;
            var end = start.AddMinutes(reservation.DurationMinutes);
            var clashes = (await _db.ReservationTables
                    .Where(x => x.ReservationId != reservation.Id && x.Reservation!.Status == ReservationStatus.Confirmed)
                    .Select(x => new
                    {
                        x.TableId,
                        x.Table!.TableNumber,
                        x.Reservation!.CustomerName,
                        x.Reservation.ReservedFor,
                        x.Reservation.DurationMinutes
                    })
                    .ToListAsync())
                .Where(x => tableIds.Contains(x.TableId)
                         && x.ReservedFor < end && start < x.ReservedFor.AddMinutes(x.DurationMinutes))
                .ToList();
            if (clashes.Count > 0)
            {
                var c = clashes[0];
                var from = BusinessClock.ToLocal(c.ReservedFor);
                var to = from.AddMinutes(c.DurationMinutes);
                throw new InvalidOperationException(
                    $"Table {c.TableNumber} is already booked {from:h:mm} – {to:h:mm tt} for {c.CustomerName}.");
            }
        }

        // Replace the current assignment
        foreach (var link in reservation.Tables.Where(t => !t.IsDeleted && !tableIds.Contains(t.TableId)))
            link.IsDeleted = true;
        var kept = reservation.Tables.Where(t => !t.IsDeleted).Select(t => t.TableId).ToHashSet();
        var tracked = _db.Entry(reservation).State != EntityState.Detached;
        foreach (var tableId in tableIds.Where(id => !kept.Contains(id)))
        {
            var link = new ReservationTable { TenantId = reservation.TenantId, ReservationId = reservation.Id, TableId = tableId };
            // On a loaded booking, add explicitly: a pre-set Guid key would otherwise be treated as an existing row
            if (tracked) _db.ReservationTables.Add(link); // EF also fixes it up into reservation.Tables
            else reservation.Tables.Add(link);
        }
    }

    private static void ReleaseTables(Reservation reservation)
    {
        foreach (var link in reservation.Tables) link.IsDeleted = true;
    }

    private static void Close(Reservation reservation, ReservationStatus status)
    {
        reservation.Status = status;
        reservation.ClosedAt = DateTime.UtcNow;
    }

    private static bool CanCustomerCancel(Reservation r) =>
        r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed && r.ReservedFor > DateTime.UtcNow;

    private PublicReservationStatusResponse BuildPublicStatus(Reservation r, Tenant tenant) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        RestaurantName = tenant.Name,
        LogoUrl = tenant.LogoUrl,
        RestaurantPhone = tenant.Phone,
        CustomerName = r.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
        PartySize = r.PartySize,
        ReservedFor = r.ReservedFor,
        Status = r.Status.ToString(),
        ResponseMessage = r.ResponseMessage,
        CanCancel = CanCustomerCancel(r)
    };

    /// <summary>Staff screens refresh on "ReservationsUpdated"; the customer's booking page follows its own group.</summary>
    private async Task BroadcastAsync(Reservation r)
    {
        await _hub.Clients.Group(r.TenantId).SendAsync("ReservationsUpdated");
        await _hub.Clients.Group(r.TenantId).SendAsync("TablesChanged");
        await _hub.Clients.Group($"reservation-{r.Id}").SendAsync("ReservationUpdated", r.Id.ToString());
    }

    private IQueryable<Reservation> Query() =>
        _db.Reservations.Include(r => r.Tables).ThenInclude(t => t.Table);

    private async Task<Reservation> LoadAsync(Guid id) =>
        await Query().FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Reservation not found.");

    private async Task<Tenant> LoadTenantAsync(string tenantId) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Restaurant not found.");

    private static ReservationSettingsResponse MapSettings(Tenant t) => new()
    {
        OnlineReservationsEnabled = t.OnlineReservationsEnabled,
        GraceMinutes = t.ReservationGraceMinutes
    };

    private static string NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
        var trimmed = mobile.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    private static void ValidateMobile(string mobile)
    {
        var digits = mobile.TrimStart('+').Length;
        if (digits < 10 || digits > 13)
            throw new InvalidOperationException("Please enter a valid mobile number.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReservationResponse Map(Reservation r) => new()
    {
        Id = r.Id,
        CustomerName = r.CustomerName,
        CustomerMobile = r.CustomerMobile,
        CustomerEmail = r.CustomerEmail,
        PartySize = r.PartySize,
        ReservedFor = r.ReservedFor,
        DurationMinutes = r.DurationMinutes,
        Notes = r.Notes,
        Status = r.Status.ToString(),
        Source = r.Source.ToString(),
        ResponseMessage = r.ResponseMessage,
        CreatedAt = r.CreatedAt,
        RespondedAt = r.RespondedAt,
        SeatedAt = r.SeatedAt,
        ClosedAt = r.ClosedAt,
        Tables = r.Tables
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Table?.TableNumber)
            .Select(t => new ReservationTableInfo
            {
                TableId = t.TableId,
                TableNumber = t.Table?.TableNumber ?? "",
                Capacity = t.Table?.Capacity ?? 0
            })
            .ToList()
    };
}
