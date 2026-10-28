namespace BookingApp.Web.Scheduling.Rules;

public sealed class MaxAdvanceRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.Resource.MaxAdvanceDays is not { } maximum)
        {
            return null;
        }

        if ((context.StartLocal.Date - context.NowLocal.Date).Days <= maximum)
            return null;

        var lastDate = context.NowLocal.Date.AddDays(maximum);
        return RuleViolation.Create("advance_limit_exceeded",
            $"Bookings can start at most {maximum} {(maximum == 1 ? "day" : "days")} ahead, up to {lastDate:d MMMM yyyy}. Start: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} office time.");
    }
}
