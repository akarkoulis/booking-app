using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public class RuleContextFactoryTests
{
    [Theory]
    [InlineData(20, 1)]
    [InlineData(27, 0)]
    public void Create_PreservesInputsAndPrecomputesEveryOfficeLocalInstant(int day, int offsetHours)
    {
        var now = new DateTime(2026, 10, day, 10, 0, 0, DateTimeKind.Utc);
        var start = now.AddHours(1);
        var end = now.AddHours(2);
        var resource = new Resource();
        var user = new User();
        var policy = new BookingPolicy();

        var context = RuleContextFactory.Create(RuleTestData.OfficeTime,
            start, end, 2, resource, user, policy, now, 3, 1);

        Assert.Equal(start, context.StartUtc);
        Assert.Equal(end, context.EndUtc);
        Assert.Equal(now, context.NowUtc);
        Assert.Equal(DateTime.SpecifyKind(start.AddHours(offsetHours), DateTimeKind.Unspecified), context.StartLocal);
        Assert.Equal(DateTime.SpecifyKind(end.AddHours(offsetHours), DateTimeKind.Unspecified), context.EndLocal);
        Assert.Equal(DateTime.SpecifyKind(now.AddHours(offsetHours), DateTimeKind.Unspecified), context.NowLocal);
        Assert.Equal(DateTimeKind.Unspecified, context.StartLocal.Kind);
        Assert.Equal(DateTimeKind.Unspecified, context.EndLocal.Kind);
        Assert.Equal(DateTimeKind.Unspecified, context.NowLocal.Kind);
        Assert.Same(resource, context.Resource);
        Assert.Same(user, context.User);
        Assert.Same(policy, context.Policy);
        Assert.Equal(2, context.Quantity);
        Assert.Equal(3L, context.ConfirmedOverlappingQuantity);
        Assert.Equal(1, context.ActiveRoomBookingCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Create_RejectsNonUtcInstants(int input)
    {
        var utc = RuleTestData.Utc(12);
        var wrongKind = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        Assert.Throws<ArgumentException>(() => RuleContextFactory.Create(RuleTestData.OfficeTime,
            input == 0 ? wrongKind : utc, input == 1 ? wrongKind : utc, 1,
            new Resource(), new User(), new BookingPolicy(), input == 2 ? wrongKind : utc, 0, 0));
    }
}
