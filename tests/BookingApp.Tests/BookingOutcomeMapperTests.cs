using BookingApp.Web.Api;
using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public sealed class BookingOutcomeMapperTests
{
    [Theory]
    [InlineData("", 201)]
    [InlineData("capacity_exceeded", 409)]
    [InlineData("invalid_interval", 422)]
    [InlineData("capacity_exceeded,invalid_interval", 422)]
    [InlineData("invalid_interval,capacity_exceeded", 422)]
    [InlineData("quantity_invalid,resource_inactive", 422)]
    public void Map_UsesCapacityConflictOnlyWhenAlone(string codes, int expected)
    {
        var violations = codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => new RuleViolation(code, "Test violation.")).ToArray();
        Assert.Equal(expected, BookingOutcomeMapper.Map(violations));
    }
}
