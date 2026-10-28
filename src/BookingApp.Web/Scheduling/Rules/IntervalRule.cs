namespace BookingApp.Web.Scheduling.Rules;

public sealed class IntervalRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        return context.StartUtc < context.EndUtc ? null : RuleViolation.Create("invalid_interval",
            $"The booking end must be after its start. Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
