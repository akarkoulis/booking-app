using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Api;
using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public sealed class RequestTimeTests
{
    [Theory]
    [InlineData("2026-10-20T14:00:00", 20, 13)]
    [InlineData("2026-10-20T13:00:00Z", 20, 13)]
    [InlineData("2026-10-20T14:00:00+01:00", 20, 13)]
    [InlineData("2026-10-20T08:00:00-05:00", 20, 13)]
    [InlineData("2026-10-27T14:00", 27, 14)]
    [InlineData("2026-10-25T01:30+01:00", 25, 0)]
    [InlineData("2026-10-25T01:30Z", 25, 1)]
    public void RequestTime_PreservesWallClockOrExplicitInstant(string value, int day, int hour)
    {
        Assert.True(RuleTestData.OfficeTime.TryParseRequest(value, "start", out var utc, out var violation));
        Assert.Null(violation);
        Assert.Equal(new DateTime(2026, 10, day, hour, day == 25 ? 30 : 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Theory]
    [InlineData("2026-03-29T01:30", "nonexistent_local_time")]
    [InlineData("2026-10-25T01:30", "ambiguous_local_time")]
    public void RequestTime_DstViolationNamesItsField(string value, string code)
    {
        Assert.False(RuleTestData.OfficeTime.TryParseRequest(value, "end", out _, out var violation));
        Assert.Equal(code, violation!.Code);
        Assert.StartsWith("end:", violation.Message);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("2026-02-30T10:00")]
    [InlineData("2026-10-20")]
    [InlineData("2026-10-20T25:00")]
    [InlineData("2026-10-20T10:00+15:00")]
    [InlineData("2026-10-20T10:00:00.12345678")]
    public void Dtos_RejectMalformedTimesWithFieldKey(string value)
    {
        foreach (var dto in new object[]
        {
            new BookingRequest { ResourceId = 1, Quantity = 1, Start = value, End = "2026-10-20T15:00" },
            new AvailabilityQuery { Start = value, End = "2026-10-20T15:00" }
        })
        {
            var errors = new List<ValidationResult>();
            Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true));
            var timeErrors = ((IValidatableObject)dto).Validate(new ValidationContext(dto));
            Assert.Contains(timeErrors, error => error.MemberNames.Contains("start"));
        }
    }

    [Fact]
    public void Dtos_AllowDstWallClocksForRuleValidation()
    {
        var request = new BookingRequest { ResourceId = 1, Quantity = 1, Start = "2026-03-29T01:30", End = "2026-03-29T02:30" };
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public void RequestTime_PreservesFractionalSeconds()
    {
        Assert.True(RuleTestData.OfficeTime.TryParseRequest("2026-10-20T14:00:00.1234567+01:00", "start", out var utc, out _));
        Assert.Equal(RuleTestData.Utc(13).AddTicks(1234567), utc);
    }

    [Fact]
    public void StoredClockTimestamp_UsesPostgresMicrosecondsForStableResponses()
    {
        var now = RuleTestData.Utc(13).AddTicks(1234567);
        var stored = OfficeTime.ToStoragePrecision(now);
        Assert.Equal(RuleTestData.Utc(13).AddTicks(1234560), stored);
        Assert.Equal(DateTimeKind.Utc, stored.Kind);
    }
}
