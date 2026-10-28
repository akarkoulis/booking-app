using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/bookings")]
public sealed class BookingsController(BookingService service, BookingQueries queries, OfficeTime officeTime) : BookingApiController
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<BookingResponse>(201, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(409, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(422, "application/problem+json")]
    public async Task<ActionResult<BookingResponse>> Create(BookingRequest request, CancellationToken cancellationToken)
    {
        var violations = RequestTimes.ToUtc(officeTime, request.Start, request.End, out var startUtc, out var endUtc);
        if (violations.Count != 0)
            return BookingProblemDetails.Result(violations);
        var result = await service.CreateAsync(CurrentUserId, request.ResourceId!.Value, startUtc, endUtc,
            request.Quantity!.Value, request.Title, cancellationToken);
        var status = BookingOutcomeMapper.Map(result.Violations);
        if (status != 201)
            return BookingProblemDetails.Result(result.Violations, status);
        var response = BookingResponse.From(result.Booking!, officeTime);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BookingResponse>>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Get([FromQuery] BookingHistoryQuery query, CancellationToken cancellationToken)
    {
        var bookings = await BookingQueries.FilterBookings(queries.VisibleBookings(CurrentUserId, IsAdmin),
                CurrentUserId, IsAdmin, query.ResourceId, query.UserId, query.ToWindow(officeTime))
            .OrderBy(b => b.StartUtc).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
        return Ok(bookings.Select(b => BookingResponse.From(b, officeTime)).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<BookingResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public async Task<ActionResult<BookingResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var booking = await queries.VisibleBookings(CurrentUserId, IsAdmin).SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        return booking is null ? Problem(statusCode: 404, title: "Booking not found.") : Ok(BookingResponse.From(booking, officeTime));
    }

    [HttpPost("{id:int}/cancel")]
    [Consumes("application/json")]
    [ProducesResponseType<BookingResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(422, "application/problem+json")]
    public async Task<ActionResult<BookingResponse>> Cancel(int id, CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(id, CurrentUserId, IsAdmin, cancellationToken);
        if (result.NotFound)
            return Problem(statusCode: 404, title: "Booking not found.");
        if (result.Violations.Count != 0)
            return BookingProblemDetails.Result(result.Violations);
        return Ok(BookingResponse.From(result.Booking!, officeTime));
    }
}
