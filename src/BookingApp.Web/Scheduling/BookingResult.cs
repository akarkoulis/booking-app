using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling;

public sealed record BookingResult(Booking? Booking, IReadOnlyList<RuleViolation> Violations, bool NotFound = false);

public sealed record ResourceAvailability(Resource Resource, IReadOnlyList<RuleViolation> Violations);
