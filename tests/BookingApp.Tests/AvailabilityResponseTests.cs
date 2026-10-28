using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

public sealed class AvailabilityResponseTests
{
    [Theory]
    [InlineData(20, 1)]
    [InlineData(27, 0)]
    public void Response_ExposesResolvedInstantsWithOfficeOffsets(int day, int offsetHours)
    {
        var office = new OfficeTime(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Office:TimeZone"] = "Europe/London" }).Build());
        var start = new DateTime(2026, 10, day, 13, 0, 0, DateTimeKind.Utc);
        var resource = new Resource { Id = 9, Name = "Room", Type = ResourceType.Room, Quantity = 1 };
        var response = AvailabilityResponse.From(resource, [], start, start.AddHours(1), office);
        Assert.Equal(start, response.Start.UtcDateTime);
        Assert.Equal(start.AddHours(1), response.End.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), response.Start.Offset);
        Assert.True(response.Available);
        var unavailable = AvailabilityResponse.From(resource, [new("capacity", "Full")], start, start.AddHours(1), office);
        Assert.False(unavailable.Available);
        Assert.Equal("Full", Assert.Single(unavailable.Violations).Message);
    }
}
