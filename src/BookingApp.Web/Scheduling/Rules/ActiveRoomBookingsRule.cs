using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling.Rules;

public sealed class ActiveRoomBookingsRule : IBookingRule
{
    public RuleViolation? Evaluate(RuleContext context)
    {
        if (context.Resource.Type != ResourceType.Room || context.Policy.MaxActiveRoomBookingsPerUser is not { } maximum)
        {
            return null;
        }

        return context.ActiveRoomBookingCount < maximum
            ? null : RuleViolation.Create("room_booking_limit_reached",
                $"You may have at most {maximum} confirmed room bookings that have not yet ended; you already have {context.ActiveRoomBookingCount}.");
    }
}
