using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Scheduling;

public sealed class BookingQueries(BookingDbContext db)
{
    public async Task<Resource?> LockResourceAsync(int resourceId, CancellationToken cancellationToken)
    {
        // Materialize before SingleOrDefault so EF does not compose over the locking SQL.
        var resources = await db.Resources.FromSql($"""
            SELECT * FROM "Resources" WHERE "Id" = {resourceId} FOR UPDATE
            """).ToListAsync(cancellationToken);
        return resources.SingleOrDefault();
    }

    public Task<Dictionary<int, long>> OverlappingQuantitiesAsync(int[] resourceIds, DateTime startUtc,
        DateTime endUtc, CancellationToken cancellationToken) => db.Bookings.AsNoTracking()
        .Where(b => resourceIds.Contains(b.ResourceId) && b.Status == BookingStatus.Confirmed
            && b.StartUtc < endUtc && startUtc < b.EndUtc)
        .GroupBy(b => b.ResourceId)
        .Select(group => new { ResourceId = group.Key, Quantity = group.Sum(b => (long)b.Quantity) })
        .ToDictionaryAsync(group => group.ResourceId, group => group.Quantity, cancellationToken);

    public Task<int> ActiveRoomCountAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.Bookings.CountAsync(b => b.UserId == userId && b.Status == BookingStatus.Confirmed
            && b.Resource.Type == ResourceType.Room && b.EndUtc > nowUtc, cancellationToken);

    public IQueryable<Booking> VisibleBookings(int userId, bool isAdmin) => db.Bookings.AsNoTracking()
        .Include(b => b.Resource).Include(b => b.User)
        .Where(b => isAdmin || b.UserId == userId);

    public static IQueryable<Booking> FilterBookings(IQueryable<Booking> bookings, int currentUserId, bool isAdmin,
        int? resourceId, int? userId, BookingHistoryWindow window)
    {
        bookings = bookings.Where(b => isAdmin || b.UserId == currentUserId);
        if (resourceId is { } resource)
            bookings = bookings.Where(b => b.ResourceId == resource);
        if (userId is { } user)
            bookings = bookings.Where(b => b.UserId == user);
        if (window.StartUtc is { } start)
            bookings = bookings.Where(b => b.EndUtc > start);
        if (window.EndUtc is { } end)
            bookings = bookings.Where(b => b.StartUtc < end);
        return bookings;
    }
}
