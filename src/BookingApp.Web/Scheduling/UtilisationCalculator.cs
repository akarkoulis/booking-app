using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling;

public sealed record UtcInterval(DateTime StartUtc, DateTime EndUtc);
public sealed record UtilisationBooking(DateTime StartUtc, DateTime EndUtc, int Quantity, BookingStatus Status);
// Decimal tick totals keep integer precision even for very large resource pools.
public sealed record UtilisationTotals(int BookingCount, decimal BookedTicks, decimal AvailableTicks);

public static class UtilisationCalculator
{
    public static UtilisationTotals Calculate(int capacity, IReadOnlyList<UtilisationBooking> bookings,
        UtcInterval window, IReadOnlyList<UtcInterval> dailyOpenIntervals)
    {
        var count = 0;
        decimal bookedTicks = 0;
        foreach (var booking in bookings)
        {
            if (booking.Status != BookingStatus.Confirmed)
                continue;
            var start = booking.StartUtc > window.StartUtc ? booking.StartUtc : window.StartUtc;
            var end = booking.EndUtc < window.EndUtc ? booking.EndUtc : window.EndUtc;
            if (end <= start)
                continue;
            count++;
            bookedTicks += (decimal)(end - start).Ticks * booking.Quantity;
        }

        decimal availableTicks = 0;
        foreach (var interval in dailyOpenIntervals)
        {
            var start = interval.StartUtc > window.StartUtc ? interval.StartUtc : window.StartUtc;
            var end = interval.EndUtc < window.EndUtc ? interval.EndUtc : window.EndUtc;
            if (end > start)
                availableTicks += (decimal)(end - start).Ticks * capacity;
        }
        return new(count, bookedTicks, availableTicks);
    }
}
