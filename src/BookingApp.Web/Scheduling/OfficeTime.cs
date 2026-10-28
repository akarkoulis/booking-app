using System.Globalization;
using System.Text.RegularExpressions;

namespace BookingApp.Web.Scheduling;

public sealed class OfficeTime(IConfiguration configuration)
{
    public const string RequestPattern = @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2}(\.[0-9]{1,7})?)?(Z|[+-][0-9]{2}:[0-9]{2})?$";
    public const string RequestDescription = "ISO date and time: yyyy-MM-ddTHH:mm, optional seconds and 1-7 fractional digits; optional Z or +/-HH:mm. Without an offset this is an office-local wall clock; gaps and ambiguous times return 422. With an offset this is an exact instant. Encode + as %2B in query strings.";
    private static readonly string[] LocalFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"];
    private static readonly string[] OffsetFormats = ["yyyy-MM-dd'T'HH:mmzzz", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    public static bool IsValidRequest(string value) => TryReadRequest(value, out _);

    // PostgreSQL timestamps have microsecond precision; return the same timestamp on write and subsequent reads.
    public static DateTime ToStoragePrecision(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC instant is required.", nameof(utc));
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    // DTO validation rejects malformed values before conversion. The only request violations here are DST rules.
    public bool TryParseRequest(string value, string field, out DateTime utc, out RuleViolation? violation)
    {
        if (!TryReadRequest(value, out var parsed))
        {
            throw new ArgumentException("Validate the request date and time before conversion.", nameof(value));
        }

        if (parsed.Kind == DateTimeKind.Utc)
        {
            utc = parsed;
            violation = null;
            return true;
        }

        if (TryToUtc(parsed, out utc, out violation))
        {
            return true;
        }

        violation = violation! with { Message = $"{field}: {violation.Message}" };
        return false;
    }

    private static bool TryReadRequest(string value, out DateTime parsed)
    {
        parsed = default;
        if (!Regex.IsMatch(value, RequestPattern, RegexOptions.CultureInvariant))
        {
            return false;
        }

        var hasOffset = value.EndsWith('Z') || value.LastIndexOf('+') > 10 || value.LastIndexOf('-') > 10;
        if (!hasOffset)
        {
            return DateTime.TryParseExact(value, LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
        }

        var offsetValue = value.EndsWith('Z') ? value[..^1] + "+00:00" : value;
        if (!DateTimeOffset.TryParseExact(offsetValue, OffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            return false;
        }

        parsed = instant.UtcDateTime;
        return true;
    }

    public bool TryToUtc(DateTime localTime, out DateTime utc, out RuleViolation? violation)
    {
        if (localTime.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("An office-local wall clock must have unspecified Kind.", nameof(localTime));
        }

        utc = default;
        var zone = GetOfficeZone();
        if (zone.IsInvalidTime(localTime))
        {
            violation = RuleViolation.Create("nonexistent_local_time",
                $"{localTime:d MMMM yyyy} {localTime:HH:mm} office time does not exist because the clocks move forward. Choose another time.");
            return false;
        }

        if (zone.IsAmbiguousTime(localTime))
        {
            violation = RuleViolation.Create("ambiguous_local_time",
                $"{localTime:d MMMM yyyy} {localTime:HH:mm} office time occurs twice because the clocks move back. Choose an unambiguous time.");
            return false;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
        violation = null;
        return true;
    }

    public DateTimeOffset ToOfficeOffset(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A UTC instant is required.", nameof(utc));
        }

        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), GetOfficeZone());
    }

    public DateTime ToOfficeLocal(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A UTC instant is required.", nameof(utc));
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utc, GetOfficeZone());
    }

    public DateTime ToUtc(DateTime localTime)
    {
        if (localTime.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("An office-local wall clock must have unspecified Kind.", nameof(localTime));
        }

        var zone = GetOfficeZone();
        if (zone.IsInvalidTime(localTime) || zone.IsAmbiguousTime(localTime))
        {
            throw new ArgumentException("The office-local time is nonexistent or ambiguous.", nameof(localTime));
        }

        return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
    }

    // Configured boundaries have no person available to choose another time. Resolve them
    // leniently; user-entered booking times must still use TryToUtc's strict validation.
    public DateTime ConfiguredTimeToUtc(DateTime localTime)
    {
        if (localTime.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException("An office-local wall clock must have unspecified Kind.", nameof(localTime));

        var zone = GetOfficeZone();
        if (zone.IsAmbiguousTime(localTime))
        {
            // The larger offset gives the earlier UTC occurrence.
            return new DateTimeOffset(localTime, zone.GetAmbiguousTimeOffsets(localTime).Max()).UtcDateTime;
        }
        if (zone.IsInvalidTime(localTime))
        {
            var beforeGap = localTime;
            do { beforeGap = beforeGap.AddMinutes(-1); } while (zone.IsInvalidTime(beforeGap));
            // Applying the pre-transition offset shifts the wall clock forward by the gap.
            return new DateTimeOffset(localTime, zone.GetUtcOffset(beforeGap)).UtcDateTime;
        }
        return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
    }

    private TimeZoneInfo GetOfficeZone()
    {
        var zoneId = configuration["Office:TimeZone"]
            ?? throw new InvalidOperationException("Office:TimeZone is required.");
        return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
    }
}
