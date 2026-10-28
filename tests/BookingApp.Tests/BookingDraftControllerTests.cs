using System.Security.Claims;
using BookingApp.Web.Ai;
using BookingApp.Web.Api;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace BookingApp.Tests;

public sealed class BookingDraftControllerTests
{
    private sealed class Parser(Func<CancellationToken, Task<BookingDraftFields>> parse) : IBookingDraftParser
    {
        public Task<BookingDraftFields> ParseAsync(string text, CancellationToken token) => parse(token);
    }

    private sealed class OutcomeLogger : ILogger<BookingDraftsController>
    {
        public string? Outcome { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Outcome = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state)
                .Single(item => item.Key == "Outcome").Value?.ToString();
        }
    }

    private static BookingDraftsController Controller(IBookingDraftParser parser, ILogger<BookingDraftsController>? logger = null)
    {
        var controller = new BookingDraftsController(parser, new BookingDraftService(null!),
            RuleTestData.OfficeTime, TimeProvider.System, logger ?? NullLogger<BookingDraftsController>.Instance);
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "test")) } };
        return controller;
    }

    [Fact]
    public async Task Partial_returns_200_without_database_access()
    {
        var result = await Controller(new Parser(_ => Task.FromResult(new BookingDraftFields { ResourceType = BookingApp.Web.Domain.ResourceType.Car })))
            .Post(new() { Text = "a car" }, default);
        var response = Assert.IsType<BookingDraftResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(response.Resource);
        Assert.Empty(response.Alternatives);
    }

    [Fact]
    public async Task Unavailable_returns_fixed_503_without_exception_detail()
    {
        var result = await Controller(new UnavailableBookingDraftParser()).Post(new() { Text = "meeting" }, default);
        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(503, problem.StatusCode);
        Assert.Equal("Drafting is unavailable; use the form below.", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    [Fact]
    public async Task Validation_returns_booking_problem_422()
    {
        var result = await Controller(new Parser(_ => throw new BookingDraftValidationException([new("draft_not_understood", "Fixed example.")])))
            .Post(new() { Text = "unrelated" }, default);
        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("draft_not_understood", Assert.Single(Assert.IsType<BookingProblemDetails>(problem.Value).Violations).Code);
    }

    [Fact]
    public async Task Client_disconnect_never_becomes_503()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(new UnavailableBookingDraftParser()).Post(new() { Text = "meeting" }, cancellation.Token));
    }

    [Theory]
    [InlineData("2026-03-29", "nonexistent_local_time")]
    [InlineData("2026-10-25", "ambiguous_local_time")]
    public async Task Complete_dst_errors_use_default_booking_problem_title(string date, string code)
    {
        var fields = BookingDraftTests.Complete with { Date = date, StartTime = "01:30", EndTime = "03:00" };
        var result = await Controller(new Parser(_ => Task.FromResult(fields))).Post(new() { Text = "meeting" }, default);
        var problem = Assert.IsType<BookingProblemDetails>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Equal(422, problem.Status);
        Assert.Equal("Booking rules were not satisfied.", problem.Title);
        Assert.Equal(code, Assert.Single(problem.Violations).Code);
    }

    [Theory]
    [InlineData("2026-03-29")]
    [InlineData("2026-10-25")]
    public async Task Partial_dst_wall_clock_returns_200_without_conversion(string date)
    {
        var fields = BookingDraftTests.Complete with { Date = date, StartTime = "01:30", EndTime = null, Location = "Stated location" };
        var result = await Controller(new Parser(_ => Task.FromResult(fields))).Post(new() { Text = "meeting" }, default);
        var response = Assert.IsType<BookingDraftResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(response.Resource);
        Assert.Empty(response.Alternatives);
        Assert.Equal("Stated location", response.Location);
    }

    [Fact]
    public async Task Unexpected_failure_is_logged_as_failed()
    {
        var logger = new OutcomeLogger();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Controller(new Parser(_ => throw new InvalidOperationException()), logger)
            .Post(new() { Text = "meeting" }, default));
        Assert.Equal("failed", logger.Outcome);
    }

    [Fact]
    public async Task Request_cancellation_is_logged_as_cancelled()
    {
        var logger = new OutcomeLogger();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(new UnavailableBookingDraftParser(), logger)
            .Post(new() { Text = "meeting" }, source.Token));
        Assert.Equal("cancelled", logger.Outcome);
    }
}
