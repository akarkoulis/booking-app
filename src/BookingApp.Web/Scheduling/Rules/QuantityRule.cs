namespace BookingApp.Web.Scheduling.Rules;

public sealed class QuantityRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        return context.Quantity >= 1 && context.Quantity <= context.Resource.Quantity
            ? null : RuleViolation.Create("invalid_quantity",
                $"Quantity must be between 1 and {context.Resource.Quantity}; requested {context.Quantity}.");
    }
}
