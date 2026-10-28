namespace BookingApp.Web.Scheduling.Rules;

public sealed class NotInPastRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        return context.StartUtc >= context.NowUtc ? null : RuleViolation.Create("start_in_past",
            $"The booking cannot start in the past. Start: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm}; now: {context.NowLocal:d MMMM yyyy} {context.NowLocal:HH:mm} office time.");
    }
}
