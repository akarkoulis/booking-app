using BookingApp.Web.Ai;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/booking-drafts")]
public sealed class BookingDraftsController(IBookingDraftParser parser, BookingDraftService drafts,
    OfficeTime officeTime, TimeProvider clock, ILogger<BookingDraftsController> logger) : BookingApiController
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<BookingDraftResponse>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(422, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
    public async Task<ActionResult<BookingDraftResponse>> Post(BookingDraftRequest request, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var outcome = "failed";
        try
        {
            var fields = await parser.ParseAsync(request.Text!.Trim(), cancellationToken);
            if (fields.Date is null || fields.StartTime is null || fields.EndTime is null ||
                (fields.ResourceType is null && string.IsNullOrWhiteSpace(fields.ResourceName)))
            {
                outcome = "parsed";
                return Ok(BookingDraftResponse.From(fields));
            }
            var violations = RequestTimes.ToUtc(officeTime, $"{fields.Date}T{fields.StartTime}", $"{fields.Date}T{fields.EndTime}",
                out var startUtc, out var endUtc);
            if (violations.Count != 0)
            {
                outcome = "not_understood_or_invalid";
                return BookingProblemDetails.Result(violations);
            }
            var selection = await drafts.ResolveAsync(CurrentUserId, startUtc, endUtc, fields.EffectiveQuantity,
                fields.ResourceType, fields.ResourceName, fields.MinSeats, cancellationToken);
            AvailabilityResponse Map(ResourceAvailability row) => AvailabilityResponse.From(row.Resource, row.Violations, startUtc, endUtc, officeTime);
            var response = BookingDraftResponse.From(fields, selection.Resource is null ? null : Map(selection.Resource),
                selection.Alternatives.Select(Map).ToArray());
            outcome = "parsed";
            return Ok(response);
        }
        catch (BookingDraftValidationException error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outcome = "not_understood_or_invalid";
            return BookingProblemDetails.Result(error.Violations, title: "The booking draft could not be understood.");
        }
        catch (BookingDraftUnavailableException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outcome = "unavailable";
            return Problem(statusCode: 503, title: "Booking drafting is unavailable.", detail: "Drafting is unavailable; use the form below.");
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested) outcome = "cancelled";
            logger.LogInformation("Booking draft outcome {Outcome} in {ElapsedMilliseconds} ms", outcome, clock.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
