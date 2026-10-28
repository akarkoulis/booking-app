using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BookingApp.Web.Api;

public sealed class BookingProblemDetails : ProblemDetails
{
    public required IReadOnlyList<RuleViolation> Violations { get; init; }

    public static ObjectResult Result(IReadOnlyList<RuleViolation> violations, int status = 422, string? title = null)
    {
        var problem = new BookingProblemDetails
        {
            Title = title ?? (status == 409 ? "Resource capacity exceeded." : "Booking rules were not satisfied."),
            Status = status,
            Violations = violations
        };
        return new BookingProblemResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    // The request context is available when MVC formats the result, without changing controller call sites.
    private sealed class BookingProblemResult : ObjectResult
    {
        public BookingProblemResult(BookingProblemDetails problem) : base(problem) { }

        public override void OnFormatting(ActionContext context)
        {
            var problem = (BookingProblemDetails)Value!;
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var defaults = factory.CreateProblemDetails(context.HttpContext, problem.Status, problem.Title);
            problem.Type = defaults.Type;
            problem.Detail = defaults.Detail;
            problem.Instance = defaults.Instance;
            foreach (var extension in defaults.Extensions)
                problem.Extensions[extension.Key] = extension.Value;
            base.OnFormatting(context);
        }
    }
}
