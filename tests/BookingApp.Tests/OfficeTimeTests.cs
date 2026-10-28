using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

public class OfficeTimeTests
{
    [Theory]
    [InlineData(20, 13)]
    [InlineData(27, 14)]
    public void TryToUtc_ConvertsAnUnambiguousWallClock(int day, int utcHour)
    {
        Assert.True(officeTime.TryToUtc(new DateTime(2026, 10, day, 14, 0, 0), out var utc, out var violation));
        Assert.Equal(new DateTime(2026, 10, day, utcHour, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Null(violation);
    }

    [Theory]
    [InlineData(3, 29, "nonexistent_local_time")]
    [InlineData(10, 25, "ambiguous_local_time")]
    public void TryToUtc_ReturnsTheClockChangeViolationWithoutChoosingAnInstant(int month, int day, string code)
    {
        Assert.False(officeTime.TryToUtc(new DateTime(2026, month, day, 1, 30, 0), out var utc, out var violation));
        Assert.Equal(default, utc);
        Assert.NotNull(violation);
        Assert.Equal(code, violation.Code);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void LocalConversions_ThrowForCallerKindErrors(DateTimeKind kind)
    {
        var input = new DateTime(2026, 10, 20, 14, 0, 0, kind);
        Assert.Throws<ArgumentException>(() => officeTime.TryToUtc(input, out _, out _));
        Assert.Throws<ArgumentException>(() => officeTime.ToUtc(input));
    }

    [Theory]
    [InlineData(20, 13, 1)]
    [InlineData(27, 14, 0)]
    public void ToOfficeOffset_PreservesTheInstantWithTheOfficeOffset(int day, int utcHour, int offsetHours)
    {
        var utc = new DateTime(2026, 10, day, utcHour, 0, 0, DateTimeKind.Utc);
        var result = officeTime.ToOfficeOffset(utc);
        Assert.Equal(utc, result.UtcDateTime);
        Assert.Equal(new DateTime(2026, 10, day, 14, 0, 0), result.DateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), result.Offset);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void ToOfficeOffset_DistinguishesTheTwoAutumnOverlapInstants(int utcHour, int offsetHours)
    {
        var utc = new DateTime(2026, 10, 25, utcHour, 30, 0, DateTimeKind.Utc);
        var result = officeTime.ToOfficeOffset(utc);
        Assert.Equal(utc, result.UtcDateTime);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0), result.DateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), result.Offset);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void UtcConversions_RejectNonUtcInstants(DateTimeKind kind)
    {
        var input = new DateTime(2026, 10, 20, 14, 0, 0, kind);
        Assert.Throws<ArgumentException>(() => officeTime.ToOfficeOffset(input));
        Assert.Throws<ArgumentException>(() => officeTime.ToOfficeLocal(input));
    }

    [Theory]
    [InlineData(20, 13)]
    [InlineData(27, 14)]
    public void ToOfficeLocal_UsesTheOfficeOffsetOnBothSidesOfTheClockChange(int day, int utcHour)
    {
        var result = officeTime.ToOfficeLocal(new DateTime(2026, 10, day, utcHour, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, day, 14, 0, 0, DateTimeKind.Unspecified), result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    private readonly OfficeTime officeTime = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Office:TimeZone"] = "Europe/London"
        }).Build());

    [Theory]
    [InlineData(20, 13)]
    [InlineData(27, 14)]
    public void ToUtc_UsesTheOfficeOffsetOnBothSidesOfTheClockChange(int day, int utcHour)
    {
        var result = officeTime.ToUtc(new DateTime(2026, 10, day, 14, 0, 0));

        Assert.Equal(new DateTime(2026, 10, day, utcHour, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData(3, 29)]
    [InlineData(10, 25)]
    public void ToUtc_RejectsNonexistentOrAmbiguousOfficeTimes(int month, int day)
    {
        Assert.Throws<ArgumentException>(() =>
            officeTime.ToUtc(new DateTime(2026, month, day, 1, 30, 0)));
    }
}
