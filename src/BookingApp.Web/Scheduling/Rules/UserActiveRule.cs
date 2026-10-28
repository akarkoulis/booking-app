namespace BookingApp.Web.Scheduling.Rules;

public sealed class UserActiveRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        return context.User.IsActive ? null : RuleViolation.Create("user_inactive",
            $"The user '{context.User.DisplayName}' is inactive and cannot make bookings.");
    }
}
