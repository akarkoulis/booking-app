using System.Globalization;

namespace BookingApp.Web.Scheduling.Rules;

public static class DurationText
{
    public static string Format(int minutes)
    {
        var hours = minutes / 60;
        var remainder = minutes % 60;
        var minuteText = string.Create(CultureInfo.InvariantCulture, $"{remainder} {(remainder == 1 ? "minute" : "minutes")}");
        if (hours == 0)
        {
            return minuteText;
        }

        var hourText = string.Create(CultureInfo.InvariantCulture, $"{hours} {(hours == 1 ? "hour" : "hours")}");
        return remainder == 0 ? hourText : hourText + " " + minuteText;
    }
}
