using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling;

// Aggregates describe existing confirmed bookings, excluding the requested booking itself.
// Active rooms include ongoing and future room bookings whose EndUtc is after NowUtc.
public sealed record RuleContext(
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,
    DateTime EndLocal,
    int Quantity,
    Resource Resource,
    User User,
    BookingPolicy Policy,
    DateTime NowUtc,
    DateTime NowLocal,
    long ConfirmedOverlappingQuantity,
    int ActiveRoomBookingCount);
