using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

public sealed class BookingHistoryTests
{
    private static OfficeTime Office() => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Office:TimeZone"] = "Europe/London" }).Build());

    [Theory]
    [InlineData("2026-10-20", "2026-10-20", 24, "2026-10-19T23:00:00Z")]
    [InlineData("2026-10-27", "2026-10-27", 24, "2026-10-27T00:00:00Z")]
    [InlineData("2026-03-29", "2026-03-29", 23, "2026-03-29T00:00:00Z")]
    [InlineData("2026-10-25", "2026-10-25", 25, "2026-10-24T23:00:00Z")]
    public void Window_UsesInclusiveOfficeDates(string from, string to, int hours, string start)
    {
        var window = new BookingHistoryQuery { From = from, To = to }.ToWindow(Office());
        Assert.Equal(DateTime.Parse(start, null, System.Globalization.DateTimeStyles.AdjustToUniversal), window.StartUtc);
        Assert.Equal(hours, (window.EndUtc!.Value - window.StartUtc!.Value).TotalHours);
        var report = new UtilisationQuery { From = from, To = to }.ToWindow(Office());
        Assert.Equal(report.Interval.StartUtc, window.StartUtc);
        Assert.Equal(report.Interval.EndUtc, window.EndUtc);
    }

    [Theory]
    [InlineData("2026-1-01", null, "from")]
    [InlineData("2026-02-30", null, "from")]
    [InlineData(null, "2026-10-32", "to")]
    [InlineData(null, "9999-12-31", "to")]
    [InlineData("2026-10-27", "2026-10-26", "from,to")]
    public void Query_RejectsInvalidDatesWithCamelCaseKeys(string? from, string? to, string keys)
    {
        var errors = new BookingHistoryQuery { From = from, To = to }.Validate(new ValidationContext(new object()));
        Assert.Equal(keys.Split(','), errors.SelectMany(e => e.MemberNames));
    }

    [Theory]
    [InlineData(0, null, "ResourceId")]
    [InlineData(null, -1, "UserId")]
    public void Query_RejectsNonpositiveIds(int? resourceId, int? userId, string member)
    {
        var query = new BookingHistoryQuery { ResourceId = resourceId, UserId = userId };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(query, new(query), errors, true));
        Assert.Equal(member, Assert.Single(Assert.Single(errors).MemberNames));
        // MVC's configured JSON metadata provider supplies camelCase on the wire.
    }

    [Fact]
    public void Query_AllowsOpenEndedWindowsAndNoHistoryLengthCap()
    {
        Assert.Null(new BookingHistoryQuery().ToWindow(Office()).StartUtc);
        Assert.Null(new BookingHistoryQuery { From = "2026-10-20" }.ToWindow(Office()).EndUtc);
        Assert.Null(new BookingHistoryQuery { To = "2026-10-20" }.ToWindow(Office()).StartUtc);
        var query = new BookingHistoryQuery { From = "2025-01-01", To = "2026-12-31" };
        Assert.Empty(query.Validate(new(query)));
    }

    private static Booking Row(int id, int user, int resource, int start, int end, BookingStatus status = BookingStatus.Confirmed) => new()
    {
        Id = id, UserId = user, ResourceId = resource, Status = status,
        StartUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc).AddHours(start),
        EndUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc).AddHours(end)
    };

    [Fact]
    public void Filters_IntersectOwnershipIdsAndHalfOpenOverlapWithoutDroppingCancelled()
    {
        Booking[] rows = [Row(1, 7, 2, -1, 1), Row(2, 8, 2, 1, 2), Row(3, 7, 3, 1, 2),
            Row(4, 7, 2, -2, 0), Row(5, 7, 2, 24, 25), Row(6, 7, 2, 23, 25, BookingStatus.Cancelled)];
        var window = new BookingHistoryQuery { From = "2026-10-27", To = "2026-10-27" }.ToWindow(Office());
        int[] Ids(bool admin, int? resource, int? user) => BookingQueries.FilterBookings(rows.AsQueryable(), 7, admin,
            resource, user, window).Select(b => b.Id).ToArray();
        Assert.Equal([1, 6], Ids(false, 2, null));
        Assert.Empty(Ids(false, null, 8));
        Assert.Equal([2], Ids(true, 2, 8));
        Assert.Equal([1, 3, 6], Ids(true, null, 7));
        Assert.Empty(Ids(true, 99, null));
    }

    [Fact]
    public void Filters_AllowNoWindowOrEitherBoundary()
    {
        Booking[] rows = [Row(1, 7, 2, -2, 0), Row(2, 7, 2, 0, 1), Row(3, 7, 2, 24, 25)];
        int[] Ids(string? from, string? to) => BookingQueries.FilterBookings(rows.AsQueryable(), 7, false,
            null, null, new BookingHistoryQuery { From = from, To = to }.ToWindow(Office())).Select(b => b.Id).ToArray();
        Assert.Equal([1, 2, 3], Ids(null, null));
        Assert.Equal([2, 3], Ids("2026-10-27", null));
        Assert.Equal([1, 2], Ids(null, "2026-10-27"));
    }
}
