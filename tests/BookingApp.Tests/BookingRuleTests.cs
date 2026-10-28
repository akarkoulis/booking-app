using System.Globalization;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using BookingApp.Web.Scheduling.Rules;

namespace BookingApp.Tests;

public class BookingRuleTests
{
    [Theory]
    [InlineData(1, "1 minute")]
    [InlineData(30, "30 minutes")]
    [InlineData(59, "59 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(90, "1 hour 30 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(480, "8 hours")]
    [InlineData(1440, "24 hours")]
    [InlineData(61, "1 hour 1 minute")]
    [InlineData(121, "2 hours 1 minute")]
    public void DurationText_FormatsWholeMinutes(int minutes, string expected)
    {
        Assert.Equal(expected, DurationText.Format(minutes));
    }

    [Fact]
    public void MinNotice_MessageUsesHours()
    {
        var context = RuleTestData.Context();
        context.Resource.MinNoticeMinutes = 1440;
        Assert.Equal("Notice of at least 24 hours is required. Start: 20 October 2026 14:00; now: 20 October 2026 13:00 office time.", new MinNoticeRule().Evaluate(context)?.Message);
    }

    [Fact]
    public void MinDuration_MessageUsesMinutes()
    {
        var context = RuleTestData.Context(endUtc: RuleTestData.Utc(13, 15));
        Assert.Equal("The booking must last at least 30 minutes. Requested: 20 October 2026 14:00 to 20 October 2026 14:15 office time.", new MinDurationRule().Evaluate(context)?.Message);
    }

    [Fact]
    public void MaxDuration_MessageUsesHours()
    {
        var context = RuleTestData.Context(endUtc: RuleTestData.Utc(22));
        context.Resource.MaxDurationMinutes = 480;
        Assert.Equal("The booking must last no more than 8 hours. Requested: 20 October 2026 14:00 to 20 October 2026 23:00 office time.", new MaxDurationRule().Evaluate(context)?.Message);
    }

    [Theory]
    [InlineData(ResourceType.Room, 1, 1, 1, "This room is already booked at this time.")]
    [InlineData(ResourceType.Room, 1, 0, 2, "Only 1 of 1 available at this time (0 already booked); 2 needed.")]
    [InlineData(ResourceType.Equipment, 4, 3, 2, "Only 1 of 4 available at this time (3 already booked); 2 needed.")]
    [InlineData(ResourceType.Car, 4, 5, 1, "Only 0 of 4 available at this time (5 already booked); 1 needed.")]
    [InlineData(ResourceType.Catering, 4, 0, 5, "Only 4 of 4 available at this time (0 already booked); 5 needed.")]
    [InlineData(ResourceType.Equipment, 4, long.MaxValue, 1, "Only 0 of 4 available at this time (9223372036854775807 already booked); 1 needed.")]
    public void Capacity_ExplainsRoomOrPoolAvailability(ResourceType type, int capacity, long booked, int requested, string message)
    {
        var context = RuleTestData.Context(confirmedOverlappingQuantity: booked, quantity: requested);
        context.Resource.Type = type;
        context.Resource.Quantity = capacity;
        var violation = new CapacityRule().Evaluate(context);
        Assert.NotNull(violation);
        Assert.Equal("capacity_exceeded", violation.Code);
        Assert.Equal(message + " Requested: 20 October 2026 14:00 to 20 October 2026 15:00 office time.", violation.Message);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(0, "invalid_interval")]
    [InlineData(-1, "invalid_interval")]
    public void Interval_RequiresStrictlyIncreasingInstants(int ticks, string? code)
    {
        var start = RuleTestData.Utc(13);
        Check(new IntervalRule(), RuleTestData.Context(startUtc: start, endUtc: start.AddTicks(ticks)), code);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, "start_in_past")]
    public void NotInPast_AllowsExactlyNow(int ticks, string? code)
    {
        var now = RuleTestData.Utc(12);
        Check(new NotInPastRule(), RuleTestData.Context(startUtc: now.AddTicks(ticks)), code);
    }

    [Theory]
    [InlineData(false, 0, null)]
    [InlineData(true, 0, null)]
    [InlineData(false, 1, "invalid_granularity")]
    [InlineData(true, 1, "invalid_granularity")]
    [InlineData(false, 10000000, "invalid_granularity")]
    [InlineData(true, 10000000, "invalid_granularity")]
    [InlineData(false, 600000000, "invalid_granularity")]
    [InlineData(true, 600000000, "invalid_granularity")]
    public void Granularity_ChecksBothLocalEndpointsIncludingFractionalSeconds(bool changeEnd, long ticks, string? code)
    {
        Check(new GranularityRule(), RuleTestData.Context(
            startUtc: RuleTestData.Utc(13).AddTicks(changeEnd ? 0 : ticks),
            endUtc: RuleTestData.Utc(14).AddTicks(changeEnd ? ticks : 0)), code);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "resource_inactive")]
    public void ResourceActive_RequiresActiveResource(bool active, string? code)
    {
        var context = RuleTestData.Context();
        context.Resource.IsActive = active;
        Check(new ResourceActiveRule(), context, code);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "user_inactive")]
    public void UserActive_RequiresActiveUser(bool active, string? code)
    {
        var context = RuleTestData.Context();
        context.User.IsActive = active;
        Check(new UserActiveRule(), context, code);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(4, null)]
    [InlineData(0, "invalid_quantity")]
    [InlineData(5, "invalid_quantity")]
    public void Quantity_IncludesBothLimits(int quantity, string? code)
    {
        Check(new QuantityRule(), RuleTestData.Context(quantity: quantity), code);
    }

    [Theory]
    [InlineData(8, 0, 18, 0, 0, null)]
    [InlineData(7, 59, 18, 0, 0, "outside_opening_hours")]
    [InlineData(8, 0, 18, 1, 0, "outside_opening_hours")]
    [InlineData(8, 0, 18, 0, 1, "outside_opening_hours")]
    public void OpeningHours_IncludeBoundsAndRequireOneLocalDay(
        int startHour, int startMinute, int endHour, int endMinute, int endDayOffset, string? code)
    {
        var start = new DateTime(2026, 10, 20, startHour, startMinute, 0);
        var end = new DateTime(2026, 10, 20, endHour, endMinute, 0).AddDays(endDayOffset);
        Check(new OpeningHoursRule(), RuleTestData.Context(
            startUtc: RuleTestData.OfficeTime.ToUtc(start), endUtc: RuleTestData.OfficeTime.ToUtc(end)), code);
    }

    [Fact]
    public void OpeningHours_NullHoursAllowAnOvernightBooking()
    {
        var context = RuleTestData.Context(endUtc: RuleTestData.Utc(14).AddDays(1));
        context.Resource.OpensAt = null;
        context.Resource.ClosesAt = null;
        Check(new OpeningHoursRule(), context, null);
    }

    [Fact]
    public void OpeningHours_MessageUsesInvariantOfficeDateAndTime()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var context = RuleTestData.Context(endUtc: RuleTestData.Utc(18));
            var violation = new OpeningHoursRule().Evaluate(context);

            Assert.NotNull(violation);
            Assert.Equal("outside_opening_hours", violation.Code);
            Assert.Equal("Bookings must start and finish on the same day, between 08:00 and 18:00. Requested: 20 October 2026 14:00 to 20 October 2026 19:00 office time.", violation.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, "duration_too_short")]
    public void MinDuration_IncludesElapsedMinimum(long ticks, string? code)
    {
        Check(new MinDurationRule(), RuleTestData.Context(endUtc: RuleTestData.Utc(13, 30).AddTicks(ticks)), code);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "duration_too_long")]
    public void MaxDuration_IncludesElapsedMaximum(long ticks, string? code)
    {
        Check(new MaxDurationRule(), RuleTestData.Context(endUtc: RuleTestData.Utc(15).AddTicks(ticks)), code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MinDuration_SkipsNonpositiveIntervals(int minutes)
    {
        Check(new MinDurationRule(), RuleTestData.Context(endUtc: RuleTestData.Utc(13).AddMinutes(minutes)), null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    public void Duration_NullLimitsAreDisabled(int minutes)
    {
        var context = RuleTestData.Context(endUtc: RuleTestData.Utc(13).AddMinutes(minutes));
        context.Resource.MinDurationMinutes = null;
        context.Resource.MaxDurationMinutes = null;
        Check(new MinDurationRule(), context, null);
        Check(new MaxDurationRule(), context, null);
    }

    [Fact]
    public void Duration_UsesElapsedMinutesAcrossAutumnClockChange()
    {
        var context = RuleTestData.Context(
            startUtc: new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc));
        context.Resource.MinDurationMinutes = 90;
        context.Resource.MaxDurationMinutes = 90;
        Check(new MinDurationRule(), context, null);
        Check(new MaxDurationRule(), context, null);
        context.Resource.MinDurationMinutes = 91;
        context.Resource.MaxDurationMinutes = 89;
        Check(new MinDurationRule(), context, "duration_too_short");
        Check(new MaxDurationRule(), context, "duration_too_long");
    }

    [Theory]
    [InlineData(30, null)]
    [InlineData(31, "advance_limit_exceeded")]
    public void MaxAdvance_IncludesFinalLocalDate(int days, string? code)
    {
        Check(new MaxAdvanceRule(), RuleTestData.Context(startUtc: RuleTestData.Utc(13).AddDays(days)), code);
    }

    [Theory]
    [InlineData(7, "Bookings can start at most 7 days ahead, up to 3 November 2026. Start: 4 November 2026 09:00 office time.")]
    [InlineData(1, "Bookings can start at most 1 day ahead, up to 28 October 2026. Start: 4 November 2026 09:00 office time.")]
    public void MaxAdvance_MessageUsesInclusiveLastDateAndSingularDay(int maximum, string message)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var context = RuleTestData.Context(
                nowUtc: new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc),
                startUtc: new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
            context.Resource.MaxAdvanceDays = maximum;
            var violation = Assert.IsType<RuleViolation>(new MaxAdvanceRule().Evaluate(context));
            Assert.Equal("advance_limit_exceeded", violation.Code);
            Assert.Equal(message, violation.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void MaxAdvance_UsesLocalCalendarDaysAcrossClockChange()
    {
        var context = RuleTestData.Context(
            nowUtc: RuleTestData.OfficeTime.ToUtc(new DateTime(2026, 10, 24, 0, 0, 0)),
            startUtc: RuleTestData.OfficeTime.ToUtc(new DateTime(2026, 10, 25, 23, 45, 0)));
        context.Resource.MaxAdvanceDays = 1;
        Check(new MaxAdvanceRule(), context, null);
    }

    [Fact]
    public void MaxAdvance_NullLimitAllowsADistantDate()
    {
        var context = RuleTestData.Context(startUtc: RuleTestData.Utc(13).AddDays(31));
        context.Resource.MaxAdvanceDays = null;
        Check(new MaxAdvanceRule(), context, null);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, "insufficient_notice")]
    public void MinNotice_IncludesExactElapsedThreshold(long ticks, string? code)
    {
        Check(new MinNoticeRule(), RuleTestData.Context(startUtc: RuleTestData.Utc(13).AddTicks(ticks)), code);
    }

    [Theory]
    [InlineData(null, 60, 59, "insufficient_notice")]
    [InlineData(null, 60, 60, null)]
    [InlineData(120, 60, 119, "insufficient_notice")]
    [InlineData(120, 60, 120, null)]
    [InlineData(30, 60, 30, null)]
    [InlineData(0, 60, 0, null)]
    [InlineData(null, null, 0, null)]
    public void MinNotice_UsesResourceThenPolicyWithZeroDisablingNotice(
        int? resourceNotice, int? policyNotice, int minutes, string? code)
    {
        var context = RuleTestData.Context(startUtc: RuleTestData.Utc(12).AddMinutes(minutes));
        context.Resource.MinNoticeMinutes = resourceNotice;
        context.Policy.DefaultMinNoticeMinutes = policyNotice;
        Check(new MinNoticeRule(), context, code);
    }

    [Theory]
    [InlineData(ResourceType.Room, 3, 2, null)]
    [InlineData(ResourceType.Room, 3, 3, "room_booking_limit_reached")]
    [InlineData(ResourceType.Car, 3, 3, null)]
    [InlineData(ResourceType.Equipment, 3, 3, null)]
    [InlineData(ResourceType.Catering, 3, 3, null)]
    [InlineData(ResourceType.Room, null, 3, null)]
    public void ActiveRoomBookings_AppliesOnlyToRoomsWithALimit(ResourceType type, int? limit, int count, string? code)
    {
        var context = RuleTestData.Context(activeRoomBookingCount: count);
        context.Resource.Type = type;
        context.Policy.MaxActiveRoomBookingsPerUser = limit;
        Check(new ActiveRoomBookingsRule(), context, code);
    }

    [Theory]
    [InlineData(2, 2, null)]
    [InlineData(3, 2, "capacity_exceeded")]
    [InlineData(0, 4, null)]
    public void Capacity_IncludesExactPooledCapacity(long existingQuantity, int requestedQuantity, string? code)
    {
        Check(new CapacityRule(), RuleTestData.Context(
            confirmedOverlappingQuantity: existingQuantity, quantity: requestedQuantity), code);
    }

    [Fact]
    public void Capacity_DoesNotOverflowAnIntegerSum()
    {
        var context = RuleTestData.Context(confirmedOverlappingQuantity: int.MaxValue, quantity: 1);
        context.Resource.Quantity = int.MaxValue;
        Check(new CapacityRule(), context, "capacity_exceeded");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Capacity_SkipsNonpositiveIntervals(int minutes)
    {
        Check(new CapacityRule(), RuleTestData.Context(
            endUtc: RuleTestData.Utc(13).AddMinutes(minutes), confirmedOverlappingQuantity: 4), null);
    }

    private static void Check(IBookingRule rule, RuleContext context, string? expectedCode)
    {
        Assert.Equal(expectedCode, rule.Evaluate(context)?.Code);
    }
}
