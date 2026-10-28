using BookingApp.Web.Scheduling.Rules;

namespace BookingApp.Web.Scheduling;

public static class BookingRules
{
    // Evaluate every rule and retain all violations in this order.
    // Step 4 maps capacity alone to 409; any other violation (including mixed capacity) to 422.
    public static IReadOnlyList<IBookingRule> All { get; } = Array.AsReadOnly<IBookingRule>(
    [
        new IntervalRule(),
        new NotInPastRule(),
        new GranularityRule(),
        new ResourceActiveRule(),
        new UserActiveRule(),
        new QuantityRule(),
        new OpeningHoursRule(),
        new MinDurationRule(),
        new MaxDurationRule(),
        new MaxAdvanceRule(),
        new MinNoticeRule(),
        new ActiveRoomBookingsRule(),
        new CapacityRule()
    ]);
}
