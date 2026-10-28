namespace BookingApp.Web.Scheduling.Rules;

public sealed class ResourceActiveRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        return context.Resource.IsActive ? null : RuleViolation.Create("resource_inactive",
            $"The resource '{context.Resource.Name}' is inactive and cannot be booked.");
    }
}
