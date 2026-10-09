using System.Text.Json;
using System.Text.Json.Serialization;

namespace menu_backend.Helpers;

/// <summary>
/// Timestamps are stored in UTC, but MySQL returns them with DateTimeKind.Unspecified, which
/// System.Text.Json writes without a "Z". Browsers then read them as local time (5:30 h off in IST).
/// This writes every DateTime as explicit UTC ("…Z"). Calendar-only values should use DateOnly.
/// </summary>
public class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        // An explicit offset ("+05:30") is parsed as server-local time; normalize to UTC.
        // Values without an offset (e.g. "2026-10-09") are left as-is: they are calendar inputs.
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        // Fixed 3-digit milliseconds: the most widely parseable ISO form (older Safari is strict)
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture));
    }
}
