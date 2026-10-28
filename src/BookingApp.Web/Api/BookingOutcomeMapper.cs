using BookingApp.Web.Scheduling;

namespace BookingApp.Web.Api;

public static class BookingOutcomeMapper
{
    public static int Map(IReadOnlyList<RuleViolation> violations) => violations.Count == 0 ? 201
        : violations.Count == 1 && violations[0].Code == "capacity_exceeded" ? 409 : 422;
}
