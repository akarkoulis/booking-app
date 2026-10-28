using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

internal static class RuleTestData
{
    public static OfficeTime OfficeTime { get; } = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Office:TimeZone"] = "Europe/London" }).Build());

    public static DateTime Utc(int hour, int minute = 0) => new(2026, 10, 20, hour, minute, 0, DateTimeKind.Utc);

    public static RuleContext Context(DateTime? startUtc = null, DateTime? endUtc = null,
        DateTime? nowUtc = null, int quantity = 1, long confirmedOverlappingQuantity = 0,
        int activeRoomBookingCount = 0)
    {
        return RuleContextFactory.Create(OfficeTime, startUtc ?? Utc(13), endUtc ?? Utc(14), quantity,
            new Resource
            {
                Name = "Projectors", Type = ResourceType.Equipment, Quantity = 4,
                OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(18, 0),
                MinDurationMinutes = 30, MaxDurationMinutes = 120, MaxAdvanceDays = 30
            },
            new User { DisplayName = "Alice" },
            new BookingPolicy { DefaultMinNoticeMinutes = 60, MaxActiveRoomBookingsPerUser = 3 },
            nowUtc ?? Utc(12), confirmedOverlappingQuantity, activeRoomBookingCount);
    }
}
