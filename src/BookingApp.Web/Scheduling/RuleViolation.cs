using System.Globalization;

namespace BookingApp.Web.Scheduling;

public sealed record RuleViolation(string Code, string Message)
{
    public static RuleViolation Create(string code, FormattableString message) =>
        new(code, message.ToString(CultureInfo.InvariantCulture));
}
