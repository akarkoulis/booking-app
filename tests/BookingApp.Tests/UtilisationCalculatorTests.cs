using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

public sealed class UtilisationCalculatorTests
{
    private readonly OfficeTime officeTime = new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Office:TimeZone"] = "Europe/London" }).Build());

    [Theory]
    [InlineData(3, 29, 1)]
    [InlineData(10, 25, 0)]
    public void ConfiguredTime_ResolvesGapForwardAndOverlapToEarlierOccurrence(int month, int day, int utcHour)
    {
        Assert.Equal(new DateTime(2026, month, day, utcHour, 30, 0, DateTimeKind.Utc),
            officeTime.ConfiguredTimeToUtc(new DateTime(2026, month, day, 1, 30, 0)));
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void ConfiguredTime_RequiresUnspecifiedKind(DateTimeKind kind) =>
        Assert.Throws<ArgumentException>(() => officeTime.ConfiguredTimeToUtc(new DateTime(2026, 10, 25, 1, 30, 0, kind)));

    [Theory]
    [InlineData(3, 29, null, 23)]
    [InlineData(10, 25, null, 25)]
    [InlineData(3, 29, "00:00", 3)]
    [InlineData(10, 25, "00:00", 5)]
    [InlineData(3, 29, "01:30", 1.5)]
    [InlineData(10, 25, "01:30", 3.5)]
    public void Availability_UsesElapsedHoursAcrossClockChanges(int month, int day, string? opens, double hours)
    {
        var date = new DateOnly(2026, month, day);
        var window = UtilisationWindow.Create(officeTime, date, date);
        var intervals = window.OpenIntervals(officeTime, opens is null ? null : TimeOnly.Parse(opens), opens is null ? null : new TimeOnly(4, 0));
        var totals = UtilisationCalculator.Calculate(1, [], window.Interval, intervals);
        Assert.Equal((decimal)TimeSpan.FromHours(hours).Ticks, totals.AvailableTicks);
    }

    [Fact]
    public void Calculate_ClipsBookingsMultipliesQuantitiesAndExcludesCancelledAndAdjacent()
    {
        var start = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        var window = new UtcInterval(start, start.AddDays(1));
        UtilisationBooking[] bookings =
        [
            new(start.AddHours(-1), start.AddHours(2), 2, BookingStatus.Confirmed),
            new(start.AddHours(23), start.AddHours(25), 3, BookingStatus.Confirmed),
            new(start, start.AddDays(1), 8, BookingStatus.Cancelled),
            new(start.AddHours(-1), start, 1, BookingStatus.Confirmed),
            new(start.AddDays(1), start.AddHours(25), 1, BookingStatus.Confirmed)
        ];
        var totals = UtilisationCalculator.Calculate(4, bookings, window, [window]);
        Assert.Equal(2, totals.BookingCount);
        Assert.Equal((decimal)TimeSpan.FromHours(7).Ticks, totals.BookedTicks);
        Assert.Equal((decimal)TimeSpan.FromHours(96).Ticks, totals.AvailableTicks);
    }

    [Fact]
    public void Calculate_HandlesLargePoolsWithoutTickOverflow()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var interval = new UtcInterval(start, start.AddDays(92));
        var totals = UtilisationCalculator.Calculate(int.MaxValue,
            [new(start, interval.EndUtc, int.MaxValue, BookingStatus.Confirmed)], interval, [interval]);
        Assert.Equal((decimal)TimeSpan.FromDays(92).Ticks * int.MaxValue, totals.AvailableTicks);
        Assert.Equal(totals.AvailableTicks, totals.BookedTicks);
    }

    [Fact]
    public void Response_RoundsOnlyAtBoundaryAndUsesUnroundedRatio()
    {
        var resource = new Resource { Id = 9, Name = "Pool", Type = ResourceType.Equipment, Quantity = 2, IsActive = false };
        var totals = new UtilisationTotals(2, TimeSpan.FromMinutes(1).Ticks, TimeSpan.FromMinutes(3).Ticks);
        var response = UtilisationResponse.From(resource, totals);
        Assert.Equal(0.02m, response.BookedHours);
        Assert.Equal(0.05m, response.AvailableHours);
        Assert.Equal(0.3333m, response.BookedUtilisation);
        Assert.False(response.IsActive);
        Assert.Equal(2, response.Quantity);
        Assert.Null(UtilisationResponse.From(resource, new(0, 0, 0)).BookedUtilisation);
    }

    [Fact]
    public void Calculate_ZeroAvailabilityDoesNotDiscardExistingBookings()
    {
        var start = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        var interval = new UtcInterval(start, start.AddHours(1));
        var totals = UtilisationCalculator.Calculate(1, [new(start, interval.EndUtc, 1, BookingStatus.Confirmed)], interval, []);
        Assert.Equal(0m, totals.AvailableTicks);
        Assert.Equal((decimal)TimeSpan.FromHours(1).Ticks, totals.BookedTicks);
    }
}
