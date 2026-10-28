using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling;

public static class RuleContextFactory
{
    // The caller captures now once and supplies database aggregates; this factory never reads a clock or database.
    public static RuleContext Create(OfficeTime officeTime, DateTime startUtc, DateTime endUtc, int quantity,
        Resource resource, User user, BookingPolicy policy, DateTime nowUtc,
        long confirmedOverlappingQuantity, int activeRoomBookingCount)
    {
        return new RuleContext(startUtc, endUtc,
            officeTime.ToOfficeLocal(startUtc), officeTime.ToOfficeLocal(endUtc),
            quantity, resource, user, policy, nowUtc, officeTime.ToOfficeLocal(nowUtc),
            confirmedOverlappingQuantity, activeRoomBookingCount);
    }
}
