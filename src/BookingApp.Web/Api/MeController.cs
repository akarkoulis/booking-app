using BookingApp.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(BookingDbContext db, IConfiguration configuration) : BookingApiController
{
    [HttpGet]
    [ProducesResponseType<MeResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == CurrentUserId, cancellationToken);
        return Ok(MeResponse.From(user, configuration));
    }
}
