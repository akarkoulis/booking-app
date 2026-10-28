using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Scheduling;

namespace BookingApp.Web.Api;

public static class RequestTimes
{
    public static IEnumerable<ValidationResult> Validate(string start, string end)
    {
        if (!string.IsNullOrWhiteSpace(start) && !OfficeTime.IsValidRequest(start))
            yield return new ValidationResult("Enter a real date and time in the documented format.", ["start"]);
        if (!string.IsNullOrWhiteSpace(end) && !OfficeTime.IsValidRequest(end))
            yield return new ValidationResult("Enter a real date and time in the documented format.", ["end"]);
    }

    public static IReadOnlyList<RuleViolation> ToUtc(OfficeTime officeTime, string start, string end,
        out DateTime startUtc, out DateTime endUtc)
    {
        var violations = new List<RuleViolation>();
        if (!officeTime.TryParseRequest(start, "start", out startUtc, out var startViolation))
            violations.Add(startViolation!);
        if (!officeTime.TryParseRequest(end, "end", out endUtc, out var endViolation))
            violations.Add(endViolation!);
        return violations;
    }
}
