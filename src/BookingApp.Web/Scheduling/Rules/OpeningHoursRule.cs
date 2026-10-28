namespace BookingApp.Web.Scheduling.Rules;

public sealed class OpeningHoursRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.Resource.OpensAt is not { } opensAt || context.Resource.ClosesAt is not { } closesAt)
        {
            return null;
        }

        return context.StartLocal.Date == context.EndLocal.Date
            && TimeOnly.FromDateTime(context.StartLocal) >= opensAt
            && TimeOnly.FromDateTime(context.EndLocal) <= closesAt
            ? null : RuleViolation.Create("outside_opening_hours",
                $"Bookings must start and finish on the same day, between {opensAt:HH:mm} and {closesAt:HH:mm}. Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
