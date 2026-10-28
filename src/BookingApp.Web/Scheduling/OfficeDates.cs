using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BookingApp.Web.Scheduling;

// Shared exact date parsing and inclusive upper-bound handling for office-date queries.
public static class OfficeDates
{
    public static bool TryParse(string? value, out DateOnly date) => DateOnly.TryParseExact(
        value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static DateOnly Parse(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly NextDay(DateOnly date) => date.AddDays(1);

    public static IEnumerable<ValidationResult> Validate(string? from, string? to, bool required = false, int? maxDays = null)
    {
        var fromValid = TryParse(from, out var fromDate);
        var toValid = TryParse(to, out var toDate);
        if (!fromValid && (required || from is not null))
            yield return new("Use an office-local date in yyyy-MM-dd format.", ["from"]);
        if (!toValid && (required || to is not null))
            yield return new("Use an office-local date in yyyy-MM-dd format.", ["to"]);
        if (toValid && toDate == DateOnly.MaxValue)
            yield return new("The end date must allow a next-day boundary.", ["to"]);
        if (fromValid && toValid && toDate < fromDate)
            yield return new("The end date must be on or after the start date.", ["from", "to"]);
        if (fromValid && toValid && maxDays is { } limit && toDate.DayNumber - fromDate.DayNumber >= limit)
            yield return new($"The inclusive range must contain 1 to {limit} days.", ["from", "to"]);
    }
}
