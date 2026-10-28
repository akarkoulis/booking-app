using System.Globalization;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling.Rules;

public sealed class CapacityRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.StartUtc >= context.EndUtc)
        {
            return null;
        }

        // Equivalent to overlap + requested <= capacity, without overflowing either integer type.
        if (context.ConfirmedOverlappingQuantity <= (long)context.Resource.Quantity - context.Quantity)
        {
            return null;
        }

        var free = Math.Max(0, (long)context.Resource.Quantity - context.ConfirmedOverlappingQuantity);
        var message = context.Resource.Type == ResourceType.Room && context.ConfirmedOverlappingQuantity > 0
            ? "This room is already booked at this time."
            : string.Create(CultureInfo.InvariantCulture,
                $"Only {free} of {context.Resource.Quantity} available at this time ({context.ConfirmedOverlappingQuantity} already booked); {context.Quantity} needed.");
        return RuleViolation.Create("capacity_exceeded",
            $"{message} Requested: {context.StartLocal:d MMMM yyyy} {context.StartLocal:HH:mm} to {context.EndLocal:d MMMM yyyy} {context.EndLocal:HH:mm} office time.");
    }
}
