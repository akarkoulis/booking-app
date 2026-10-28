using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BookingApp.Web.Ai;
using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public sealed class BookingDraftTests
{
    public static string Json(BookingDraftFields fields) => JsonSerializer.Serialize(fields, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
    public static BookingDraftFields Complete => new() { Date = "2026-10-30", StartTime = "09:00", EndTime = "10:30", ResourceType = ResourceType.Room };

    [Theory]
    [InlineData(null, "Enter a booking request.")]
    [InlineData("", "Enter a booking request.")]
    [InlineData("  \t", "Enter a booking request.")]
    public void Request_rejects_blank_text(string? text, string message)
    {
        var errors = new List<ValidationResult>();
        var request = new BookingDraftRequest { Text = text };
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, e => e.ErrorMessage == message && e.MemberNames.Contains("Text"));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Request_enforces_original_length(int length, bool valid)
    {
        var request = new BookingDraftRequest { Text = new string('a', length) };
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        if (!valid) Assert.Equal("The booking request must be 500 characters or fewer.", Assert.Single(errors).ErrorMessage);
    }

    [Fact]
    public void Decoder_accepts_complete_and_partial()
    {
        Assert.Equal(Complete, BookingDraftOutput.Read(Json(Complete), "stop", null));
        var partial = new BookingDraftFields { ResourceType = ResourceType.Car, NotUnderstood = "Add times" };
        Assert.Equal(partial, BookingDraftOutput.Read(Json(partial), "stop", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("model says something")]
    public void All_nine_null_ignores_notUnderstood(string? explanation)
    {
        var error = Assert.Throws<BookingDraftValidationException>(() => BookingDraftOutput.Read(Json(new() { NotUnderstood = explanation }), "stop", null));
        Assert.Equal("draft_not_understood", Assert.Single(error.Violations).Code);
        Assert.Equal("We could not understand that as a booking request. Try: Book a room for 6 people tomorrow from 14:00 to 15:30.", Assert.Single(error.Violations).Message);
        Assert.DoesNotContain("model says", error.Message);
    }

    [Fact]
    public void Refusal_precedes_nonstop_and_uses_fixed_message()
    {
        var error = Assert.Throws<BookingDraftValidationException>(() => BookingDraftOutput.Read("bad", "content_filter", "secret refusal"));
        Assert.Equal("draft_not_understood", Assert.Single(error.Violations).Code);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Equal("We could not understand that as a booking request. Try: Book a room for 6 people tomorrow from 14:00 to 15:30.", Assert.Single(error.Violations).Message);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("tool_calls")]
    [InlineData("content_filter")]
    public void Nonstop_is_unavailable(string reason) => Assert.Throws<BookingDraftUnavailableException>(() => BookingDraftOutput.Read(Json(Complete), reason, null));

    [Theory]
    [InlineData("no json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Malformed_schema_is_unavailable(string json) => Assert.Throws<BookingDraftUnavailableException>(() => BookingDraftOutput.Read(json, "stop", null));

    [Theory]
    [InlineData("\"quantity\":null", "\"quantity\":\"2\"")]
    [InlineData("\"resourceType\":\"Room\"", "\"resourceType\":\"Spaceship\"")]
    [InlineData("\"title\":null", "\"title\":null,\"resourceId\":1")]
    public void Wrong_types_and_extra_fields_are_unavailable(string original, string replacement) =>
        Assert.Throws<BookingDraftUnavailableException>(() => BookingDraftOutput.Read(Json(Complete).Replace(original, replacement), "stop", null));

    [Fact]
    public void Nonpositive_counts_and_long_titles_are_fixed_validation_errors()
    {
        var error = Assert.Throws<BookingDraftValidationException>(() => BookingDraftOutput.Read(Json(Complete with { Quantity = 0, MinSeats = -1, Title = new string('a', 201) }), "stop", null));
        Assert.Equal(3, error.Violations.Count);
        Assert.All(error.Violations, item => Assert.Equal("draft_invalid_value", item.Code));
    }

    [Theory]
    [InlineData("date", "2026-02-30")]
    [InlineData("date", "2026-1-30")]
    [InlineData("startTime", "9:00")]
    [InlineData("endTime", "24:00")]
    [InlineData("startTime", "09:00:00")]
    [InlineData("endTime", "10:00Z")]
    public void Invalid_model_times_are_422_even_when_partial(string field, string value)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(Json(new BookingDraftFields { Title = "Meeting" }))!;
        data[field] = value;
        var error = Assert.Throws<BookingDraftValidationException>(() => BookingDraftOutput.Read(JsonSerializer.Serialize(data), "stop", null));
        Assert.Equal("draft_invalid_date_time", Assert.Single(error.Violations).Code);
    }

    [Theory]
    [InlineData("2026-10-20", 8)]
    [InlineData("2026-10-27", 9)]
    public void Complete_times_are_utc(string date, int hour)
    {
        var violations = RequestTimes.ToUtc(RuleTestData.OfficeTime, $"{date}T09:00", $"{date}T10:30", out var start, out _);
        Assert.Empty(violations);
        Assert.Equal(hour, start.Hour);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    private static ResourceAvailability Row(int id, string name, ResourceType type = ResourceType.Room, int seats = 6, int quantity = 1, bool available = true) =>
        new(new Resource { Id = id, Name = name, Type = type, Seats = seats, Quantity = quantity }, available ? [] : [new("capacity_exceeded", "Full")]);

    private static BookingDraftSelection Select(BookingDraftFields fields, IReadOnlyList<ResourceAvailability> rows) =>
        BookingDraftService.Select(fields.ResourceType, fields.ResourceName, fields.MinSeats, rows);

    [Fact]
    public void Exact_name_is_case_insensitive_and_unavailable_is_retained()
    {
        var fields = Complete with { ResourceName = "tHaMeS" };
        var result = Select(fields, [Row(1, "Thames", available: false), Row(2, "Small"), Row(3, "Car", ResourceType.Car)]);
        Assert.Equal(1, result.Resource!.Resource.Id);
        Assert.Equal("Full", Assert.Single(result.Resource.Violations).Message);
        Assert.Equal(2, Assert.Single(result.Alternatives).Resource.Id);
    }

    [Fact]
    public void Unknown_name_without_type_never_falls_back_across_types()
    {
        var result = Select(Complete with { ResourceType = null, ResourceName = "Missing" }, [Row(1, "Thames")]);
        Assert.Null(result.Resource);
        Assert.Empty(result.Alternatives);
    }

    [Fact]
    public void Name_with_null_type_resolves_and_alternatives_stay_in_type()
    {
        var result = Select(Complete with { ResourceType = null, ResourceName = "projectors" },
            [Row(1, "Thames"), Row(2, "Projectors", ResourceType.Equipment, quantity: 4), Row(3, "Portable", ResourceType.Equipment, quantity: 2)]);
        Assert.Equal(2, result.Resource!.Resource.Id);
        Assert.Equal(3, Assert.Single(result.Alternatives).Resource.Id);
    }

    [Fact]
    public void Known_type_fallback_ignores_location_and_pool_minSeats_orders_smallest()
    {
        var result = Select(Complete with { ResourceType = ResourceType.Equipment, ResourceName = "projector", MinSeats = 99, Location = "Unknown" },
            [Row(1, "Large", ResourceType.Equipment, quantity: 8), Row(2, "Small", ResourceType.Equipment, quantity: 2)]);
        Assert.Equal(2, result.Resource!.Resource.Id);
        Assert.Equal(1, Assert.Single(result.Alternatives).Resource.Id);
        Assert.Equal(1, Complete.EffectiveQuantity);
    }

    [Fact]
    public void Rooms_fit_seats_and_alternatives_cap_at_three_available_only()
    {
        var result = Select(Complete with { MinSeats = 5 },
            [Row(1, "Tiny", seats: 2), Row(2, "Full", available: false), Row(3, "C", seats: 8), Row(4, "A"), Row(5, "B"), Row(6, "D", seats: 10), Row(7, "E", seats: 12)]);
        Assert.Equal(4, result.Resource!.Resource.Id);
        Assert.Equal(new[] { 5, 3, 6 }, result.Alternatives.Select(r => r.Resource.Id));
    }

    [Fact]
    public void Unknown_room_seats_sort_after_even_the_largest_known_room()
    {
        var unknown = Row(1, "A unknown");
        unknown.Resource.Seats = null;
        var result = Select(Complete, [unknown, Row(2, "Z known", seats: int.MaxValue)]);
        Assert.Equal(2, result.Resource!.Resource.Id);
    }
}
