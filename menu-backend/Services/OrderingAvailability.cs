using System.Globalization;
using System.Text.Json;
using menu_backend.Data;
using menu_backend.DTOs.Website;
using menu_backend.Helpers;
using menu_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Services;

/// <summary>Whether customers can order by QR right now, and why not.</summary>
public record OrderingStatus(bool Open, string? ClosedMessage, string? OpensAt);

/// <summary>
/// Rules for customer (QR) orders: opening hours and the distance check. Staff orders skip both.
/// Opening hours come from the restaurant website's operating hours.
/// </summary>
public class OrderingAvailability
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] TimeFormats = { "h:mm tt", "hh:mm tt", "h tt", "h:mmtt", "htt", "H:mm", "HH:mm" };

    /// <summary>GPS indoors is often off by tens of metres; give the phone this much benefit of the doubt.</summary>
    private const double MaxAccuracyAllowanceMeters = 100;

    private readonly AppDbContext _db;

    public OrderingAvailability(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OrderingStatus> GetStatusAsync(Tenant tenant)
    {
        if (!tenant.QrOrderingHoursEnabled) return new(true, null, null);
        if (tenant.OrderingOpenUntil > DateTime.UtcNow) return new(true, null, null);

        var json = await _db.WebsiteContents.IgnoreQueryFilters()
            .Where(w => w.TenantId == tenant.TenantId && !w.IsDeleted)
            .Select(w => w.OperatingHoursJson)
            .FirstOrDefaultAsync();
        List<OperatingHourDto>? hours = null;
        try { if (!string.IsNullOrEmpty(json)) hours = JsonSerializer.Deserialize<List<OperatingHourDto>>(json, JsonOpts); }
        catch (JsonException) { /* unreadable hours: don't lock anyone out */ }

        return Evaluate(hours, BusinessClock.Now, tenant.LastOrderMinutes);
    }

    /// <summary>Throws InvalidOperationException (customer-facing message) when a QR order isn't allowed.</summary>
    public async Task EnsureCustomerCanOrderAsync(Tenant tenant, double? latitude, double? longitude, double? accuracyMeters)
    {
        var status = await GetStatusAsync(tenant);
        if (!status.Open)
            throw new InvalidOperationException(status.ClosedMessage ?? "We're not taking orders right now.");

        if (!LocationCheckOn(tenant)) return;
        if (latitude == null || longitude == null)
            throw new InvalidOperationException("Please allow location access to order. We only take QR orders from inside the restaurant.");

        var distance = Geo.DistanceMeters(tenant.Latitude!.Value, tenant.Longitude!.Value, latitude.Value, longitude.Value);
        var allowance = Math.Min(Math.Max(accuracyMeters ?? 0, 0), MaxAccuracyAllowanceMeters);
        if (distance - allowance > tenant.GeofenceRadiusMeters)
            throw new InvalidOperationException("You seem to be away from the restaurant. QR orders can only be placed from inside.");
    }

    public static bool LocationCheckOn(Tenant tenant) =>
        tenant.GeofenceRadiusMeters > 0 && tenant.Latitude != null && tenant.Longitude != null;

    /// <summary>
    /// Open if now falls in today's hours, or in yesterday's when they run past midnight. A day whose
    /// times can't be read counts as open. Ordering stops <paramref name="lastOrderMinutes"/> before closing.
    /// </summary>
    public static OrderingStatus Evaluate(List<OperatingHourDto>? hours, DateTime nowLocal, int lastOrderMinutes)
    {
        if (hours == null || hours.Count == 0) return new(true, null, null);

        var today = nowLocal.Date;
        foreach (var day in new[] { today.AddDays(-1), today })
        {
            var window = WindowFor(hours, day, out var unreadable);
            if (unreadable && day == today) return new(true, null, null);
            if (window is var (open, close) && nowLocal >= open && nowLocal < close.AddMinutes(-lastOrderMinutes))
                return new(true, null, null);
        }

        // Closed: when do we open next?
        for (var i = 0; i <= 7; i++)
        {
            var day = today.AddDays(i);
            if (WindowFor(hours, day, out _) is var (open, _) && open > nowLocal)
            {
                var when = i == 0 ? $"today at {open:h:mm tt}"
                    : i == 1 ? $"tomorrow at {open:h:mm tt}"
                    : $"{open:dddd} at {open:h:mm tt}";
                return new(false, $"We're closed for orders right now. We open {when}. You can still browse the menu.", open.ToString("o"));
            }
        }
        return new(false, "We're closed for orders right now. You can still browse the menu.", null);
    }

    /// <summary>Local opening and closing time for a calendar day; closing after midnight rolls to the next day.</summary>
    private static (DateTime Open, DateTime Close)? WindowFor(List<OperatingHourDto> hours, DateTime day, out bool unreadable)
    {
        unreadable = false;
        var entry = hours.FirstOrDefault(h => string.Equals(h.Day, day.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
        if (entry == null || entry.IsClosed) return null;
        if (!TryParseTime(entry.OpenTime, out var open) || !TryParseTime(entry.CloseTime, out var close))
        {
            unreadable = true;
            return null;
        }
        var openAt = day + open;
        var closeAt = day + close;
        if (closeAt <= openAt) closeAt = closeAt.AddDays(1);
        return (openAt, closeAt);
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().ToUpperInvariant().Replace(".", "");
        if (DateTime.TryParseExact(cleaned, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }
        return false;
    }
}

public static class Geo
{
    /// <summary>Great-circle distance in metres (haversine).</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
