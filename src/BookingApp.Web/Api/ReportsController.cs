using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(BookingDbContext db, OfficeTime officeTime) : BookingApiController
{
    [HttpGet("utilisation")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<UtilisationResponse>>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<UtilisationResponse>>> GetUtilisation([FromQuery] UtilisationQuery query,
        CancellationToken cancellationToken)
    {
        var window = query.ToWindow(officeTime);
        var resources = await db.Resources.AsNoTracking().Where(r => query.Type == null || r.Type == query.Type)
            .OrderBy(r => r.Name).ThenBy(r => r.Id).ToListAsync(cancellationToken);
        var ids = resources.Select(r => r.Id).ToArray();
        var bookings = await db.Bookings.AsNoTracking().Where(b => ids.Contains(b.ResourceId)
                && b.Status == BookingStatus.Confirmed && b.StartUtc < window.Interval.EndUtc && b.EndUtc > window.Interval.StartUtc)
            .Select(b => new { b.ResourceId, Usage = new UtilisationBooking(b.StartUtc, b.EndUtc, b.Quantity, b.Status) })
            .ToListAsync(cancellationToken);
        var byResource = bookings.ToLookup(b => b.ResourceId, b => b.Usage);
        var rows = resources.Select(resource => UtilisationResponse.From(resource,
            UtilisationCalculator.Calculate(resource.Quantity, byResource[resource.Id].ToArray(), window.Interval,
                window.OpenIntervals(officeTime, resource.OpensAt, resource.ClosesAt)))).ToArray();
        return Ok(rows);
    }
}
