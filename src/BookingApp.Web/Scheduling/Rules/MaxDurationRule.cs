namespace BookingApp.Web.Scheduling.Rules;

public sealed class MaxDurationRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.Resource.MaxDurationMinutes is not { } maximum)
        {
            return null;
        }

        return context.EndUtc - context.StartUtc <= TimeSpan.FromMinutes(maximum)
            ? null : RuleViolation.Create("duration_too_long",
                $"The booking must last no more than {DurationText.Format(maximum)}. Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
