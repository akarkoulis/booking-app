namespace BookingApp.Web.Scheduling.Rules;

public sealed class GranularityRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        var intervalTicks = TimeSpan.FromMinutes(15).Ticks;
        return context.StartLocal.TimeOfDay.Ticks % intervalTicks == 0
            && context.EndLocal.TimeOfDay.Ticks % intervalTicks == 0
            ? null : RuleViolation.Create("invalid_granularity",
                $"Start and end must be on exact 15-minute boundaries. Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
