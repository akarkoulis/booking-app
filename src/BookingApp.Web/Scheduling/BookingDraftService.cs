using BookingApp.Web.Domain;

namespace BookingApp.Web.Scheduling;

public sealed record BookingDraftSelection(ResourceAvailability? Resource, IReadOnlyList<ResourceAvailability> Alternatives);

public sealed class BookingDraftService(BookingService bookings)
{
    public async Task<BookingDraftSelection> ResolveAsync(int userId, DateTime startUtc, DateTime endUtc,
        int quantity, ResourceType? resourceType, string? resourceName, int? minSeats, CancellationToken cancellationToken)
    {
        // Load without the seat filter so an exact active name can still be returned with reasons.
        var rows = await bookings.AvailabilityAsync(userId, startUtc, endUtc, quantity,
            resourceType, null, cancellationToken);
        return Select(resourceType, resourceName, minSeats, rows);
    }

    public static BookingDraftSelection Select(ResourceType? resourceType, string? resourceName, int? minSeats,
        IReadOnlyList<ResourceAvailability> rows)
    {
        var matching = rows.Where(row => resourceType is null || row.Resource.Type == resourceType).ToArray();
        var named = matching.OrderBy(row => row.Resource.Id).FirstOrDefault(row =>
            string.Equals(row.Resource.Name, resourceName, StringComparison.OrdinalIgnoreCase));
        var type = resourceType ?? named?.Resource.Type;
        if (type is null) return new(null, []);
        bool Fits(ResourceAvailability row) => type != ResourceType.Room || minSeats is null || row.Resource.Seats >= minSeats;
        var available = matching.Where(row => row.Resource.Type == type && row.Violations.Count == 0 && Fits(row))
            .OrderBy(row => type == ResourceType.Room && row.Resource.Seats is null)
            .ThenBy(row => type == ResourceType.Room ? row.Resource.Seats : row.Resource.Quantity)
            .ThenBy(row => row.Resource.Name, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Resource.Id).ToArray();
        if (named is not null && !Fits(named))
            named = named with { Violations = [.. named.Violations, new("insufficient_seats", "This room does not have enough seats.")] };
        var resource = named ?? available.FirstOrDefault();
        return new(resource, available.Where(row => row.Resource.Id != resource?.Resource.Id).Take(3).ToArray());
    }
}
