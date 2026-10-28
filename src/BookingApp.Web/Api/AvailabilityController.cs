using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/availability")]
public sealed class AvailabilityController(BookingService service, OfficeTime officeTime) : BookingApiController
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AvailabilityResponse>>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(422, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<AvailabilityResponse>>> Get([FromQuery] AvailabilityQuery query, CancellationToken cancellationToken)
    {
        var violations = RequestTimes.ToUtc(officeTime, query.Start, query.End, out var startUtc, out var endUtc);
        if (violations.Count != 0)
            return BookingProblemDetails.Result(violations);
        var resources = await service.AvailabilityAsync(CurrentUserId, startUtc, endUtc, query.Quantity,
            query.Type, query.MinSeats, cancellationToken);
        return Ok(resources.Select(item => AvailabilityResponse.From(item.Resource, item.Violations, startUtc, endUtc, officeTime)).ToArray());
    }
}
