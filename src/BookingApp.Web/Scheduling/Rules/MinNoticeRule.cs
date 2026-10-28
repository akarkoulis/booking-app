namespace BookingApp.Web.Scheduling.Rules;

public sealed class MinNoticeRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        var notice = context.Resource.MinNoticeMinutes ?? context.Policy.DefaultMinNoticeMinutes;
        if (notice is null or 0)
        {
            return null;
        }

        return context.StartUtc - context.NowUtc >= TimeSpan.FromMinutes(notice.Value)
            ? null : RuleViolation.Create("insufficient_notice",
                $"Notice of at least {DurationText.Format(notice.Value)} is required. Start: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm}; now: {context.NowLocal:d MMMM yyyy} {context.NowLocal:HH:mm} office time.");
    }
}
