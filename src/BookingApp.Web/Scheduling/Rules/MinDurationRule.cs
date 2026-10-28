namespace BookingApp.Web.Scheduling.Rules;

public sealed class MinDurationRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.StartUtc >= context.EndUtc || context.Resource.MinDurationMinutes is not { } minimum)
        {
            return null;
        }

        return context.EndUtc - context.StartUtc >= TimeSpan.FromMinutes(minimum)
            ? null : RuleViolation.Create("duration_too_short",
                $"The booking must last at least {DurationText.Format(minimum)}. Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
