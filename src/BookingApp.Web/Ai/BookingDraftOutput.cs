using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookingApp.Web.Scheduling;

namespace BookingApp.Web.Ai;

public static class BookingDraftOutput
{
    private static readonly string[] Names = ["date", "startTime", "endTime", "resourceType", "resourceName", "location", "minSeats", "quantity", "title", "notUnderstood"];
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static BookingDraftFields Read(string? json, string finishReason, string? refusal)
    {
        if (refusal is not null) throw NotUnderstood();
        if (finishReason != "stop" || string.IsNullOrWhiteSpace(json)) throw new BookingDraftUnavailableException();
        BookingDraftFields fields;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != Names.Length ||
                Names.Any(name => !root.TryGetProperty(name, out _)))
                throw new BookingDraftUnavailableException();
            fields = JsonSerializer.Deserialize<BookingDraftFields>(json, Options)!;
        }
        catch (JsonException) { throw new BookingDraftUnavailableException(); }

        if (fields.Date is null && fields.StartTime is null && fields.EndTime is null && fields.ResourceType is null &&
            fields.ResourceName is null && fields.Location is null && fields.MinSeats is null && fields.Quantity is null && fields.Title is null)
            throw NotUnderstood();

        var violations = new List<RuleViolation>();
        if (fields.Date is not null && (fields.Date.Length != 10 || !DateOnly.TryParseExact(fields.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            violations.Add(new("draft_invalid_date_time", "date: Enter a real date in yyyy-MM-dd format."));
        foreach (var (name, value) in new[] { ("startTime", fields.StartTime), ("endTime", fields.EndTime) })
        {
            if (value is not null && (value.Length != 5 || !TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                violations.Add(new("draft_invalid_date_time", $"{name}: Enter a time in HH:mm format."));
        }
        if (fields.MinSeats is <= 0) violations.Add(new("draft_invalid_value", "Minimum seats must be at least 1."));
        if (fields.Quantity is <= 0) violations.Add(new("draft_invalid_value", "Quantity must be at least 1."));
        if (fields.Title?.Length > 200) violations.Add(new("draft_invalid_value", "The title must be 200 characters or fewer."));
        if (violations.Count != 0) throw new BookingDraftValidationException(violations);
        return fields;
    }

    private static BookingDraftValidationException NotUnderstood() => new([new("draft_not_understood",
        "We could not understand that as a booking request. Try: Book a room for 6 people tomorrow from 14:00 to 15:30.")]);
}
