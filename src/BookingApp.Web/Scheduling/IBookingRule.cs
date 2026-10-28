namespace BookingApp.Web.Scheduling;

public interface IBookingRule
{
    RuleViolation? Evaluate(RuleContext context);
}
