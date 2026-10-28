using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Scheduling;

public sealed class BookingService(BookingDbContext db, BookingQueries queries, OfficeTime officeTime, TimeProvider clock)
{
    public async Task<BookingResult> CreateAsync(int userId, int resourceId, DateTime startUtc, DateTime endUtc,
        int quantity, string? title, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var policy = await db.BookingPolicies.SingleAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var resource = await queries.LockResourceAsync(resourceId, cancellationToken);
        if (resource is null)
            return new BookingResult(null, [new RuleViolation("resource_not_found", "The requested resource does not exist.")]);

        var now = OfficeTime.ToStoragePrecision(clock.GetUtcNow().UtcDateTime);
        var overlapping = await queries.OverlappingQuantitiesAsync([resourceId], startUtc, endUtc, cancellationToken);
        var activeRooms = await queries.ActiveRoomCountAsync(userId, now, cancellationToken);
        var context = RuleContextFactory.Create(officeTime, startUtc, endUtc, quantity, resource, user,
            policy, now, overlapping.GetValueOrDefault(resourceId), activeRooms);
        var violations = Evaluate(context);
        if (violations.Count != 0)
            return new BookingResult(null, violations);

        var booking = new Booking
        {
            Resource = resource, User = user, StartUtc = startUtc, EndUtc = endUtc,
            Quantity = quantity, Title = title, CreatedAtUtc = now
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookingResult(booking, []);
    }

    public async Task<BookingResult> CancelAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var resourceId = await db.Bookings.AsNoTracking().Where(b => b.Id == id && (isAdmin || b.UserId == userId))
            .Select(b => (int?)b.ResourceId).SingleOrDefaultAsync(cancellationToken);
        if (resourceId is null)
            return new BookingResult(null, [], NotFound: true);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await queries.LockResourceAsync(resourceId.Value, cancellationToken);
        var now = OfficeTime.ToStoragePrecision(clock.GetUtcNow().UtcDateTime);
        var booking = await db.Bookings.Include(b => b.Resource).Include(b => b.User)
            .SingleOrDefaultAsync(b => b.Id == id && (isAdmin || b.UserId == userId), cancellationToken);
        if (booking is null)
            return new BookingResult(null, [], NotFound: true);

        if (booking.Status == BookingStatus.Cancelled)
            return new BookingResult(booking, []);
        if (booking.EndUtc <= now)
            return new BookingResult(null, [new RuleViolation("booking_ended", "A booking that has already ended cannot be cancelled.")]);

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookingResult(booking, []);
    }

    public async Task<IReadOnlyList<ResourceAvailability>> AvailabilityAsync(int userId, DateTime startUtc,
        DateTime endUtc, int quantity, ResourceType? type, int? minSeats, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);
        var policy = await db.BookingPolicies.AsNoTracking().SingleAsync(cancellationToken);
        var resources = await db.Resources.AsNoTracking()
            .Where(r => r.IsActive && (type == null || r.Type == type) && (minSeats == null || r.Seats >= minSeats))
            .OrderBy(r => r.Name).ThenBy(r => r.Id).ToListAsync(cancellationToken);
        var overlapping = await queries.OverlappingQuantitiesAsync(resources.Select(r => r.Id).ToArray(), startUtc, endUtc, cancellationToken);
        var activeRooms = await queries.ActiveRoomCountAsync(userId, now, cancellationToken);
        return resources.Select(resource => new ResourceAvailability(resource,
            Evaluate(RuleContextFactory.Create(officeTime, startUtc, endUtc, quantity, resource, user,
                policy, now, overlapping.GetValueOrDefault(resource.Id), activeRooms)))).ToArray();
    }

    private static IReadOnlyList<RuleViolation> Evaluate(RuleContext context) => BookingRules.All
        .Select(rule => rule.Evaluate(context)).OfType<RuleViolation>().ToArray();
}
