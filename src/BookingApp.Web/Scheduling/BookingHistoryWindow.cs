namespace BookingApp.Web.Scheduling;

public sealed record BookingHistoryWindow(DateTime? StartUtc, DateTime? EndUtc)
{
    public static BookingHistoryWindow Create(OfficeTime officeTime, string? from, string? to) => new(
        from is null ? null : officeTime.ConfiguredTimeToUtc(OfficeDates.Parse(from).ToDateTime(TimeOnly.MinValue)),
        to is null ? null : officeTime.ConfiguredTimeToUtc(OfficeDates.NextDay(OfficeDates.Parse(to)).ToDateTime(TimeOnly.MinValue)));
}
