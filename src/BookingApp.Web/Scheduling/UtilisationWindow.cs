namespace BookingApp.Web.Scheduling;

public sealed record UtilisationWindow(DateOnly From, DateOnly To, UtcInterval Interval)
{
    public static UtilisationWindow Create(OfficeTime officeTime, DateOnly from, DateOnly to) => new(from, to,
        new(officeTime.ConfiguredTimeToUtc(from.ToDateTime(TimeOnly.MinValue)),
            officeTime.ConfiguredTimeToUtc(OfficeDates.NextDay(to).ToDateTime(TimeOnly.MinValue))));

    public IReadOnlyList<UtcInterval> OpenIntervals(OfficeTime officeTime, TimeOnly? opensAt, TimeOnly? closesAt)
    {
        var intervals = new List<UtcInterval>();
        for (var day = From; day <= To; day = day.AddDays(1))
        {
            var start = day.ToDateTime(opensAt ?? TimeOnly.MinValue);
            var end = closesAt is { } closes ? day.ToDateTime(closes) : day.AddDays(1).ToDateTime(TimeOnly.MinValue);
            intervals.Add(new(officeTime.ConfiguredTimeToUtc(start), officeTime.ConfiguredTimeToUtc(end)));
        }
        return intervals;
    }
}
