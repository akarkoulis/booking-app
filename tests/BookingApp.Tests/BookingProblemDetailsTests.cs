using BookingApp.Web.Api;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookingApp.Tests;

public class BookingProblemDetailsTests
{
    [Theory]
    [InlineData(409, "Resource capacity exceeded.")]
    [InlineData(422, "Booking rules were not satisfied.")]
    public void CustomProblemsUseFrameworkMappingAndTraceId(int status, string title)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "booking-test-trace" };
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        RuleViolation[] violations = [new("test_rule", "Keep this message.")];
        var result = BookingProblemDetails.Result(violations, status);

        result.OnFormatting(context);

        var problem = Assert.IsType<BookingProblemDetails>(result.Value);
        var mapping = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.ClientErrorMapping;
        Assert.Equal(mapping[status].Link, problem.Type);
        Assert.Equal("booking-test-trace", problem.Extensions["traceId"]);
        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);
        Assert.Same(violations, problem.Violations);
        Assert.Contains("application/problem+json", result.ContentTypes);
    }
}
