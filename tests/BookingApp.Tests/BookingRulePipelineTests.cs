using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public class BookingRulePipelineTests
{
    [Fact]
    public void All_ContainsEveryRuleOnceInTheDeclaredOrder()
    {
        Assert.Equal(new[]
        {
            "IntervalRule", "NotInPastRule", "GranularityRule", "ResourceActiveRule", "UserActiveRule",
            "QuantityRule", "OpeningHoursRule", "MinDurationRule", "MaxDurationRule", "MaxAdvanceRule",
            "MinNoticeRule", "ActiveRoomBookingsRule", "CapacityRule"
        }, BookingRules.All.Select(rule => rule.GetType().Name));
    }

    [Fact]
    public void All_AcceptsAValidRequest()
    {
        Assert.Empty(Evaluate(RuleTestData.Context()));
    }

    [Fact]
    public void All_ReturnsEveryViolationInStableOrderIncludingCapacity()
    {
        var context = RuleTestData.Context(confirmedOverlappingQuantity: 4);
        context.Resource.IsActive = false;
        context.User.IsActive = false;

        Assert.Equal(new[] { "resource_inactive", "user_inactive", "capacity_exceeded" }, Evaluate(context));
    }

    [Fact]
    public void All_ReturnsCapacityAloneForAnOtherwiseValidRequest()
    {
        Assert.Equal(new[] { "capacity_exceeded" }, Evaluate(RuleTestData.Context(confirmedOverlappingQuantity: 4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void All_NonpositiveIntervalDoesNotProduceDurationOrCapacityViolations(int minutes)
    {
        var context = RuleTestData.Context(endUtc: RuleTestData.Utc(13).AddMinutes(minutes),
            confirmedOverlappingQuantity: 4);

        Assert.Equal(new[] { "invalid_interval" }, Evaluate(context));
    }

    private static string[] Evaluate(RuleContext context) => BookingRules.All
        .Select(rule => rule.Evaluate(context))
        .OfType<RuleViolation>()
        .Select(violation => violation.Code)
        .ToArray();
}
