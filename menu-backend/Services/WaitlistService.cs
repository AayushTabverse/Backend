using menu_backend.Data;
using menu_backend.DTOs.Waitlist;
using menu_backend.Helpers;
using menu_backend.Hubs;
using menu_backend.Models;
using menu_backend.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace menu_backend.Services;

public class WaitlistService : IWaitlistService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IMemoryCache _cache;

    private static readonly WaitlistStatus[] OpenStatuses =
        { WaitlistStatus.Waiting, WaitlistStatus.Notified, WaitlistStatus.Seated };

    private static readonly OrderStatus[] ActiveOrderStatuses =
    {
        OrderStatus.Pending, OrderStatus.Accepted,
        OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served
    };

    public WaitlistService(AppDbContext db, ITenantProvider tenantProvider, IHubContext<OrderHub> hub, IMemoryCache cache)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _hub = hub;
        _cache = cache;
    }

    public async Task<List<WaitlistEntryResponse>> GetEntriesAsync(bool includeClosedToday)
    {
        var startOfToday = BusinessClock.StartOfDayUtc(BusinessClock.Today);

        var entries = await EntryQuery()
            .Where(e => OpenStatuses.Contains(e.Status)
                     || (includeClosedToday && e.ClosedAt != null && e.ClosedAt >= startOfToday))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        var result = entries.Select(Map).ToList();

        // Estimated wait for each party still in line, by queue position
        var waiting = result.Where(e => e.Status is nameof(WaitlistStatus.Waiting) or nameof(WaitlistStatus.Notified)).ToList();
        if (waiting.Count > 0)
        {
            var estimator = await GetEstimatorAsync(_tenantProvider.TenantId!);
            for (var i = 0; i < waiting.Count; i++)
                waiting[i].EstimatedWaitMinutes = estimator.ForPosition(i + 1);
        }

        return result;
    }

    public async Task<WaitlistEntryResponse> AddEntryAsync(CreateWaitlistEntryRequest request)
    {
        var entry = await CreateEntryAsync(_tenantProvider.TenantId!, request.CustomerName, request.CustomerMobile,
            request.PartySize, request.Notes, request.QuotedWaitMinutes, WaitlistSource.Staff);
        return Map(entry);
    }

    public async Task<WaitlistEntryResponse> UpdateEntryAsync(Guid id, UpdateWaitlistEntryRequest request)
    {
        var entry = await LoadAsync(id);

        if (request.CustomerName != null) entry.CustomerName = request.CustomerName.Trim();
        if (request.CustomerMobile != null) entry.CustomerMobile = NormalizeMobile(request.CustomerMobile);
        if (request.PartySize.HasValue) entry.PartySize = request.PartySize.Value;
        if (request.Notes != null) entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (request.QuotedWaitMinutes.HasValue) entry.QuotedWaitMinutes = request.QuotedWaitMinutes.Value;

        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return Map(entry);
    }

    public async Task<WaitlistEntryResponse> NotifyAsync(Guid id)
    {
        var entry = await LoadAsync(id);
        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
            throw new InvalidOperationException("Only waiting parties can be called.");

        entry.Status = WaitlistStatus.Notified;
        entry.NotifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return Map(entry);
    }

    public async Task<WaitlistEntryResponse> SeatAsync(Guid id, SeatWaitlistEntryRequest request)
    {
        var entry = await LoadAsync(id);
        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
            throw new InvalidOperationException("This party is no longer waiting.");

        await SeatEntryAsync(entry, request.TableIds, request.Force);
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return Map(await LoadAsync(id));
    }

    public async Task<Guid> SeatReservationAsync(Reservation reservation, List<Guid> tableIds, bool force)
    {
        var entry = new WaitlistEntry
        {
            TenantId = reservation.TenantId,
            CustomerName = reservation.CustomerName,
            CustomerMobile = reservation.CustomerMobile,
            PartySize = reservation.PartySize,
            Notes = reservation.Notes,
            Source = WaitlistSource.Reservation,
            Status = WaitlistStatus.Waiting
        };

        // Validates before anything is added, so a rejected seat leaves no stray entry
        await SeatEntryAsync(entry, tableIds, force);
        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return entry.Id;
    }

    /// <summary>
    /// Checks the tables are free and seat the party, then links them to the entry. Several tables
    /// are merged into the first (main) one. Caller saves.
    /// </summary>
    private async Task SeatEntryAsync(WaitlistEntry entry, List<Guid> requestedTableIds, bool force)
    {
        var tableIds = requestedTableIds.Distinct().ToList();
        if (tableIds.Count == 0)
            throw new InvalidOperationException("Select at least one table.");

        // A restaurant has few tables; filter in memory (the MySQL provider can't translate Guid-list Contains)
        var allTables = await _db.Tables
            .Select(t => new
            {
                Table = t,
                HasActiveOrders = t.Orders.Any(o => !o.IsDeleted && ActiveOrderStatuses.Contains(o.Status))
            })
            .ToListAsync();
        var tables = tableIds
            .Select(id => allTables.FirstOrDefault(t => t.Table.Id == id))
            .ToList();

        if (tables.Any(t => t == null))
            throw new KeyNotFoundException("One or more tables were not found.");

        var inactive = tables.Where(t => !t!.Table.IsActive).Select(t => t!.Table.TableNumber).ToList();
        if (inactive.Count > 0)
            throw new InvalidOperationException($"Table {string.Join(", ", inactive)} is inactive.");

        var busy = tables.Where(t => t!.HasActiveOrders).Select(t => t!.Table.TableNumber).ToList();
        if (busy.Count > 0)
            throw new InvalidOperationException($"Table {string.Join(", ", busy)} is occupied.");

        var mergedAway = tables.Where(t => t!.Table.MergedIntoTableId != null).Select(t => t!.Table.TableNumber).ToList();
        if (mergedAway.Count > 0)
            throw new InvalidOperationException($"Table {string.Join(", ", mergedAway)} is merged with another table.");

        var held = (await HeldTablesQuery()
            .Select(x => new { x.TableId, x.Table!.TableNumber, x.WaitlistEntry!.CustomerName })
            .ToListAsync())
            .Where(x => tableIds.Contains(x.TableId))
            .ToList();
        if (held.Count > 0)
            throw new InvalidOperationException(
                $"Table {string.Join(", ", held.Select(h => h.TableNumber))} is already held for {held[0].CustomerName}.");

        // Seats include tables already merged into the chosen ones
        var capacity = tables.Sum(t => t!.Table.Capacity)
            + allTables.Where(t => t.Table.MergedIntoTableId != null && tableIds.Contains(t.Table.MergedIntoTableId.Value))
                       .Sum(t => t.Table.Capacity);
        if (capacity < entry.PartySize && !force)
            throw new InvalidOperationException(
                $"Selected tables seat {capacity}, but the party has {entry.PartySize}. Add another table or seat anyway.");

        var mainId = tableIds[0];
        for (var i = 0; i < tableIds.Count; i++)
        {
            _db.WaitlistEntryTables.Add(new WaitlistEntryTable
            {
                TenantId = entry.TenantId,
                WaitlistEntryId = entry.Id,
                TableId = tableIds[i],
                IsPrimary = i == 0
            });
            if (i > 0) tables[i]!.Table.MergedIntoTableId = mainId;
        }

        entry.Status = WaitlistStatus.Seated;
        entry.SeatedAt = DateTime.UtcNow;
    }

    public async Task<WaitlistEntryResponse> ReleaseAsync(Guid id)
    {
        var entry = await LoadAsync(id);
        if (entry.Status != WaitlistStatus.Seated)
            throw new InvalidOperationException("This party is not seated.");

        Close(entry, WaitlistStatus.Completed);
        await FinishSeatedAsync(new[] { entry });
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);
        await _hub.Clients.Group(entry.TenantId).SendAsync("TablesChanged");

        return Map(entry);
    }

    public Task<WaitlistEntryResponse> CancelAsync(Guid id) => CloseWaitingAsync(id, WaitlistStatus.Cancelled);

    public Task<WaitlistEntryResponse> MarkNoShowAsync(Guid id) => CloseWaitingAsync(id, WaitlistStatus.NoShow);

    public async Task<WaitlistStatsResponse> GetStatsAsync()
    {
        var tenantId = _tenantProvider.TenantId!;
        var startOfToday = BusinessClock.StartOfDayUtc(BusinessClock.Today);

        var waiting = await _db.WaitlistEntries
            .Where(e => e.Status == WaitlistStatus.Waiting || e.Status == WaitlistStatus.Notified)
            .Select(e => e.PartySize)
            .ToListAsync();

        var today = await _db.WaitlistEntries
            .Where(e => e.Source != WaitlistSource.Reservation)
            .Where(e => e.CreatedAt >= startOfToday || (e.ClosedAt != null && e.ClosedAt >= startOfToday))
            .Select(e => new { e.Status, e.CreatedAt, e.SeatedAt })
            .ToListAsync();

        var seatedToday = today.Where(e => e.SeatedAt != null && e.SeatedAt >= startOfToday).ToList();
        var estimator = await GetEstimatorAsync(tenantId);

        return new WaitlistStatsResponse
        {
            Waiting = waiting.Count,
            PeopleWaiting = waiting.Sum(),
            SeatedToday = seatedToday.Count,
            CancelledToday = today.Count(e => e.Status == WaitlistStatus.Cancelled),
            NoShowToday = today.Count(e => e.Status == WaitlistStatus.NoShow),
            AverageWaitMinutesToday = seatedToday.Count == 0
                ? null
                : (int)Math.Round(seatedToday.Average(e => (e.SeatedAt!.Value - e.CreatedAt).TotalMinutes)),
            EstimatedWaitForNewParty = estimator.ForPosition(waiting.Count + 1)
        };
    }

    public async Task ReleaseTableAsync(Guid tableId)
    {
        var entries = await _db.WaitlistEntries
            .Where(e => e.Status == WaitlistStatus.Seated && e.Tables.Any(t => !t.IsDeleted && t.TableId == tableId))
            .ToListAsync();

        if (entries.Count == 0) return;

        foreach (var entry in entries)
            Close(entry, WaitlistStatus.Completed);
        await FinishSeatedAsync(entries);

        await _db.SaveChangesAsync();
        foreach (var entry in entries)
            await BroadcastAsync(entry);
    }

    /// <summary>
    /// A seated party left: un-merge the tables merged for them and complete their reservation, if any.
    /// Caller saves.
    /// </summary>
    private async Task FinishSeatedAsync(IEnumerable<WaitlistEntry> entries)
    {
        var entryIds = entries.Select(e => e.Id).ToList();
        var links = (await _db.WaitlistEntryTables.ToListAsync()).Where(x => entryIds.Contains(x.WaitlistEntryId)).ToList();
        var mainIds = links.Where(x => x.IsPrimary).Select(x => x.TableId).ToList();
        var secondaryIds = links.Where(x => !x.IsPrimary).Select(x => x.TableId).ToList();

        var merged = (await _db.Tables.Where(t => t.MergedIntoTableId != null).ToListAsync())
            .Where(t => secondaryIds.Contains(t.Id) && mainIds.Contains(t.MergedIntoTableId!.Value));
        foreach (var t in merged)
            t.MergedIntoTableId = null;

        var reservations = (await _db.Reservations.Where(r => r.Status == ReservationStatus.Seated && r.WaitlistEntryId != null).ToListAsync())
            .Where(r => entryIds.Contains(r.WaitlistEntryId!.Value));
        foreach (var r in reservations)
        {
            r.Status = ReservationStatus.Completed;
            r.ClosedAt = DateTime.UtcNow;
        }
    }

    // ── Settings ──

    public async Task<WaitlistSettingsResponse> GetSettingsAsync()
    {
        var tenant = await LoadTenantAsync(_tenantProvider.TenantId!);
        return MapSettings(tenant);
    }

    public async Task<WaitlistSettingsResponse> UpdateSettingsAsync(UpdateWaitlistSettingsRequest request)
    {
        var tenant = await LoadTenantAsync(_tenantProvider.TenantId!);

        if (request.SelfCheckInEnabled.HasValue) tenant.WaitlistSelfCheckInEnabled = request.SelfCheckInEnabled.Value;
        if (request.NoShowMinutes.HasValue) tenant.WaitlistNoShowMinutes = request.NoShowMinutes.Value;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapSettings(tenant);
    }

    // ── Analytics ──

    public async Task<WaitlistAnalyticsResponse> GetAnalyticsAsync(DateTime from, DateTime to)
    {
        var (start, end) = BusinessClock.DayRangeUtc(from, to);

        var rows = await _db.WaitlistEntries
            .Where(e => e.Source != WaitlistSource.Reservation)
            .Where(e => e.CreatedAt >= start && e.CreatedAt < end)
            .Select(e => new { e.CreatedAt, e.NotifiedAt, e.SeatedAt, e.Status, e.PartySize, e.Source })
            .ToListAsync();

        static int? AvgWait(IEnumerable<double> waits)
        {
            var list = waits.ToList();
            return list.Count == 0 ? null : (int)Math.Round(list.Average());
        }

        static double WaitOf(DateTime created, DateTime? seated) => (seated!.Value - created).TotalMinutes;

        var seated = rows.Where(r => r.SeatedAt != null).ToList();
        var waits = seated.Select(r => WaitOf(r.CreatedAt, r.SeatedAt)).OrderBy(w => w).ToList();
        var called = rows.Count(r => r.NotifiedAt != null);
        var noShows = rows.Count(r => r.Status == WaitlistStatus.NoShow);
        var cancelled = rows.Count(r => r.Status == WaitlistStatus.Cancelled);

        var byHour = rows
            .GroupBy(r => BusinessClock.ToLocal(r.CreatedAt).Hour)
            .OrderBy(g => g.Key)
            .Select(g => new WaitlistHourBucket
            {
                Hour = g.Key,
                Parties = g.Count(),
                AverageWaitMinutes = AvgWait(g.Where(r => r.SeatedAt != null).Select(r => WaitOf(r.CreatedAt, r.SeatedAt)))
            })
            .ToList();

        static string SizeLabel(int size) => size <= 2 ? "1–2" : size <= 4 ? "3–4" : size <= 6 ? "5–6" : "7+";
        var sizeOrder = new[] { "1–2", "3–4", "5–6", "7+" };
        var byPartySize = sizeOrder
            .Select(label => new { label, group = rows.Where(r => SizeLabel(r.PartySize) == label).ToList() })
            .Where(x => x.group.Count > 0)
            .Select(x => new WaitlistPartySizeBucket
            {
                Label = x.label,
                Parties = x.group.Count,
                Seated = x.group.Count(r => r.SeatedAt != null),
                AverageWaitMinutes = AvgWait(x.group.Where(r => r.SeatedAt != null).Select(r => WaitOf(r.CreatedAt, r.SeatedAt)))
            })
            .ToList();

        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(BusinessClock.ToLocal(r.CreatedAt)))
            .OrderBy(g => g.Key)
            .Select(g => new WaitlistDayBucket
            {
                Date = g.Key,
                Parties = g.Count(),
                Seated = g.Count(r => r.SeatedAt != null),
                AverageWaitMinutes = AvgWait(g.Where(r => r.SeatedAt != null).Select(r => WaitOf(r.CreatedAt, r.SeatedAt)))
            })
            .ToList();

        return new WaitlistAnalyticsResponse
        {
            From = from.Date,
            To = to.Date,
            TotalParties = rows.Count,
            TotalGuests = rows.Sum(r => r.PartySize),
            Seated = seated.Count,
            Cancelled = cancelled,
            NoShows = noShows,
            SelfCheckIns = rows.Count(r => r.Source == WaitlistSource.SelfCheckIn),
            AverageWaitMinutes = AvgWait(waits),
            MedianWaitMinutes = waits.Count == 0 ? null : (int)Math.Round(waits[waits.Count / 2]),
            NoShowRate = called == 0 ? 0 : Math.Round(100.0 * noShows / called, 1),
            WalkAwayRate = rows.Count == 0 ? 0 : Math.Round(100.0 * cancelled / rows.Count, 1),
            BusiestHour = byHour.Count == 0 ? null : byHour.MaxBy(h => h.Parties)!.Hour,
            ByHour = byHour,
            ByPartySize = byPartySize,
            ByDay = byDay
        };
    }

    // ── Public (customer-facing) ──
    // No tenant claim on these requests, so every query is scoped to the tenant explicitly.

    public async Task<PublicWaitlistInfoResponse> GetPublicInfoAsync(string tenantId)
    {
        var tenant = await LoadTenantAsync(tenantId);
        var waiting = await _db.WaitlistEntries
            .CountAsync(e => e.TenantId == tenantId && (e.Status == WaitlistStatus.Waiting || e.Status == WaitlistStatus.Notified));
        var estimator = await GetEstimatorAsync(tenantId);

        return new PublicWaitlistInfoResponse
        {
            RestaurantName = tenant.Name,
            LogoUrl = tenant.LogoUrl,
            SelfCheckInEnabled = tenant.WaitlistSelfCheckInEnabled,
            PartiesWaiting = waiting,
            EstimatedWaitMinutes = estimator.ForPosition(waiting + 1)
        };
    }

    public async Task<PublicWaitlistStatusResponse> JoinAsync(string tenantId, PublicJoinWaitlistRequest request)
    {
        var tenant = await LoadTenantAsync(tenantId);
        if (!tenant.WaitlistSelfCheckInEnabled)
            throw new InvalidOperationException("Online check-in isn't available here. Please ask the staff at the entrance.");

        var mobile = NormalizeMobile(request.CustomerMobile);
        var digits = mobile?.TrimStart('+').Length ?? 0;
        if (digits < 10 || digits > 13)
            throw new InvalidOperationException("Please enter a valid mobile number.");

        // Same phone already in line → give them their existing spot instead of a duplicate
        var existing = await _db.WaitlistEntries
            .Where(e => e.TenantId == tenantId && e.CustomerMobile == mobile
                     && (e.Status == WaitlistStatus.Waiting || e.Status == WaitlistStatus.Notified))
            .FirstOrDefaultAsync();
        if (existing != null)
            return await BuildPublicStatusAsync(existing, tenant);

        var entry = await CreateEntryAsync(tenantId, request.CustomerName, mobile, request.PartySize,
            notes: null, quotedWaitMinutes: null, WaitlistSource.SelfCheckIn);
        return await BuildPublicStatusAsync(entry, tenant);
    }

    public async Task<PublicWaitlistStatusResponse> GetPublicStatusAsync(Guid id)
    {
        var entry = await LoadAsync(id);
        var tenant = await LoadTenantAsync(entry.TenantId);
        return await BuildPublicStatusAsync(entry, tenant);
    }

    public async Task<PublicWaitlistStatusResponse> LeaveAsync(Guid id)
    {
        var entry = await LoadAsync(id);
        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
            throw new InvalidOperationException("You're no longer in the waiting line.");

        Close(entry, WaitlistStatus.Cancelled);
        entry.Notes = string.IsNullOrEmpty(entry.Notes) ? "Left via status page" : $"{entry.Notes} · Left via status page";
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        var tenant = await LoadTenantAsync(entry.TenantId);
        return await BuildPublicStatusAsync(entry, tenant);
    }

    // ── Auto no-show ──

    public async Task<int> ExpireNoShowsAsync()
    {
        var timeouts = await _db.Tenants
            .Where(t => t.WaitlistNoShowMinutes > 0)
            .Select(t => new { t.TenantId, t.WaitlistNoShowMinutes })
            .ToDictionaryAsync(t => t.TenantId, t => t.WaitlistNoShowMinutes);
        if (timeouts.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var called = await _db.WaitlistEntries
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Status == WaitlistStatus.Notified && e.NotifiedAt != null)
            .ToListAsync();

        var expired = called
            .Where(e => timeouts.TryGetValue(e.TenantId, out var minutes) && e.NotifiedAt!.Value.AddMinutes(minutes) <= now)
            .ToList();
        if (expired.Count == 0) return 0;

        foreach (var entry in expired)
        {
            Close(entry, WaitlistStatus.NoShow);
            entry.Notes = string.IsNullOrEmpty(entry.Notes) ? "Auto no-show" : $"{entry.Notes} · Auto no-show";
        }

        await _db.SaveChangesAsync();
        foreach (var entry in expired)
            await BroadcastAsync(entry);

        return expired.Count;
    }

    // ── Helpers ──

    private async Task<WaitlistEntry> CreateEntryAsync(string tenantId, string name, string? mobile, int partySize,
        string? notes, int? quotedWaitMinutes, WaitlistSource source)
    {
        var startOfToday = BusinessClock.StartOfDayUtc(BusinessClock.Today);

        var lastToken = await _db.WaitlistEntries
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.CreatedAt >= startOfToday)
            .MaxAsync(e => (int?)e.TokenNumber) ?? 0;

        var entry = new WaitlistEntry
        {
            TenantId = tenantId,
            CustomerName = name.Trim(),
            CustomerMobile = NormalizeMobile(mobile),
            PartySize = partySize,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            QuotedWaitMinutes = quotedWaitMinutes,
            TokenNumber = lastToken + 1,
            Status = WaitlistStatus.Waiting,
            Source = source
        };

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return entry;
    }

    private async Task<PublicWaitlistStatusResponse> BuildPublicStatusAsync(WaitlistEntry entry, Tenant tenant)
    {
        var response = new PublicWaitlistStatusResponse
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            RestaurantName = tenant.Name,
            LogoUrl = tenant.LogoUrl,
            TokenNumber = entry.TokenNumber,
            CustomerName = entry.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
            PartySize = entry.PartySize,
            Status = entry.Status.ToString(),
            CreatedAt = entry.CreatedAt,
            NotifiedAt = entry.NotifiedAt,
            TableNumbers = entry.Tables.Where(t => !t.IsDeleted).OrderByDescending(t => t.IsPrimary)
                .Select(t => t.Table?.TableNumber ?? "").ToList()
        };

        if (entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified)
        {
            var ahead = await _db.WaitlistEntries.CountAsync(e =>
                e.TenantId == entry.TenantId
                && (e.Status == WaitlistStatus.Waiting || e.Status == WaitlistStatus.Notified)
                && e.CreatedAt < entry.CreatedAt);
            response.Position = ahead + 1;
            response.EstimatedWaitMinutes = entry.Status == WaitlistStatus.Notified
                ? 0
                : (await GetEstimatorAsync(entry.TenantId)).ForPosition(ahead + 1);
        }

        return response;
    }

    private async Task<WaitlistEntryResponse> CloseWaitingAsync(Guid id, WaitlistStatus status)
    {
        var entry = await LoadAsync(id);
        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
            throw new InvalidOperationException("This party is no longer waiting.");

        Close(entry, status);
        await _db.SaveChangesAsync();
        await BroadcastAsync(entry);

        return Map(entry);
    }

    private static void Close(WaitlistEntry entry, WaitlistStatus status)
    {
        entry.Status = status;
        entry.ClosedAt = DateTime.UtcNow;
    }

    /// <summary>Table links of parties currently seated — these tables are held.</summary>
    private IQueryable<WaitlistEntryTable> HeldTablesQuery() =>
        _db.WaitlistEntryTables.Where(x => x.WaitlistEntry!.Status == WaitlistStatus.Seated);

    private IQueryable<WaitlistEntry> EntryQuery() =>
        _db.WaitlistEntries.Include(e => e.Tables).ThenInclude(t => t.Table);

    private async Task<WaitlistEntry> LoadAsync(Guid id) =>
        await EntryQuery().FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Waitlist entry not found.");

    private async Task<Tenant> LoadTenantAsync(string tenantId) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Restaurant not found.");

    private static WaitlistSettingsResponse MapSettings(Tenant tenant) => new()
    {
        SelfCheckInEnabled = tenant.WaitlistSelfCheckInEnabled,
        NoShowMinutes = tenant.WaitlistNoShowMinutes
    };

    private static string? NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;
        var trimmed = mobile.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    /// <summary>
    /// Staff screens refresh on "WaitlistUpdated"; customers' status pages follow their own entry
    /// and the restaurant's queue (positions shift whenever anyone ahead moves).
    /// </summary>
    private async Task BroadcastAsync(WaitlistEntry entry)
    {
        await _hub.Clients.Group(entry.TenantId).SendAsync("WaitlistUpdated");
        await _hub.Clients.Group($"waitlist-{entry.Id}").SendAsync("WaitlistEntryUpdated", entry.Id.ToString());
        await _hub.Clients.Group($"waitlist-queue-{entry.TenantId}").SendAsync("WaitlistQueueChanged");
    }

    // ── Wait-time estimate ──

    /// <summary>
    /// Estimates wait by queue position: parties beyond the number of free tables wait
    /// (position − free tables) × average minutes between seatings, rounded up to 5 minutes.
    /// </summary>
    private sealed class WaitEstimator(int freeTables, double minutesPerSeating)
    {
        public int ForPosition(int position)
        {
            var ahead = position - freeTables;
            if (ahead <= 0) return 0;
            var minutes = ahead * minutesPerSeating;
            return (int)Math.Ceiling(minutes / 5.0) * 5;
        }
    }

    private async Task<WaitEstimator> GetEstimatorAsync(string tenantId)
    {
        var minutesPerSeating = await _cache.GetOrCreateAsync($"waitlist-pace-{tenantId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await ComputeMinutesPerSeatingAsync(tenantId);
        });

        var tables = await _db.Tables
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive && t.MergedIntoTableId == null)
            .Select(t => new
            {
                t.Id,
                Busy = t.Orders.Any(o => !o.IsDeleted && ActiveOrderStatuses.Contains(o.Status))
            })
            .ToListAsync();
        var heldIds = await _db.WaitlistEntryTables
            .Where(x => x.TenantId == tenantId && x.WaitlistEntry!.Status == WaitlistStatus.Seated)
            .Select(x => x.TableId)
            .ToListAsync();
        var free = tables.Count(t => !t.Busy && !heldIds.Contains(t.Id));

        return new WaitEstimator(free, minutesPerSeating);
    }

    /// <summary>
    /// Average minutes between one table freeing up and the next. Uses the live seating pace
    /// (last 90 minutes) when there's enough of it; otherwise typical table turnover over the
    /// last 14 days spread across the active tables.
    /// </summary>
    private async Task<double> ComputeMinutesPerSeatingAsync(string tenantId)
    {
        var now = DateTime.UtcNow;

        var recentSeatings = await _db.WaitlistEntries
            .Where(e => e.TenantId == tenantId && e.SeatedAt != null && e.SeatedAt >= now.AddMinutes(-90))
            .Select(e => e.SeatedAt!.Value)
            .ToListAsync();
        if (recentSeatings.Count >= 3)
        {
            var span = (now - recentSeatings.Min()).TotalMinutes;
            return Math.Clamp(span / recentSeatings.Count, 1, 60);
        }

        var activeTables = await _db.Tables
            .IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive);

        // One "sitting" = all orders billed together on a table
        var since = now.AddDays(-14);
        var sittings = (await _db.Orders
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && !o.IsDeleted && o.Type == OrderType.DineIn
                     && o.Status == OrderStatus.Completed && o.BillNumber != null
                     && o.CompletedAt != null && o.CompletedAt >= since)
            .Select(o => new { o.TableId, o.BillNumber, o.CreatedAt, CompletedAt = o.CompletedAt!.Value })
            .ToListAsync())
            .GroupBy(o => new { o.TableId, o.BillNumber })
            .Select(g => (g.Max(o => o.CompletedAt) - g.Min(o => o.CreatedAt)).TotalMinutes)
            .Where(m => m >= 10 && m <= 240)
            .OrderBy(m => m)
            .ToList();

        var turnover = sittings.Count >= 5 ? sittings[sittings.Count / 2] : 45;
        return Math.Clamp(turnover / Math.Max(activeTables, 1), 1, 60);
    }

    private static WaitlistEntryResponse Map(WaitlistEntry e) => new()
    {
        Id = e.Id,
        TokenNumber = e.TokenNumber,
        CustomerName = e.CustomerName,
        CustomerMobile = e.CustomerMobile,
        PartySize = e.PartySize,
        Notes = e.Notes,
        QuotedWaitMinutes = e.QuotedWaitMinutes,
        Status = e.Status.ToString(),
        Source = e.Source.ToString(),
        CreatedAt = e.CreatedAt,
        NotifiedAt = e.NotifiedAt,
        SeatedAt = e.SeatedAt,
        ClosedAt = e.ClosedAt,
        Tables = e.Tables
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.IsPrimary)
            .ThenBy(t => t.Table?.TableNumber)
            .Select(t => new WaitlistTableInfo
            {
                TableId = t.TableId,
                TableNumber = t.Table?.TableNumber ?? "",
                Capacity = t.Table?.Capacity ?? 0,
                IsPrimary = t.IsPrimary
            })
            .ToList()
    };
}
