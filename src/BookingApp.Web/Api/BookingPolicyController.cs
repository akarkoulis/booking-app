using BookingApp.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/booking-policy")]
public sealed class BookingPolicyController(BookingDbContext db) : BookingApiController
{
    [HttpGet]
    [ProducesResponseType<BookingPolicyResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<ActionResult<BookingPolicyResponse>> Get(CancellationToken cancellationToken)
    {
        var policy = await db.BookingPolicies.AsNoTracking().SingleAsync(p => p.Id == 1, cancellationToken);
        return Ok(BookingPolicyResponse.From(policy));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<BookingPolicyResponse>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<ActionResult<BookingPolicyResponse>> Update(BookingPolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await db.BookingPolicies.SingleAsync(p => p.Id == 1, cancellationToken);
        policy.MaxActiveRoomBookingsPerUser = request.MaxActiveRoomBookingsPerUser;
        policy.DefaultMinNoticeMinutes = request.DefaultMinNoticeMinutes;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(BookingPolicyResponse.From(policy));
    }
}
