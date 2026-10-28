using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/resources")]
public sealed class ResourcesController(BookingDbContext db, BookingQueries queries) : BookingApiController
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ResourceResponse>>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ResourceResponse>>> Get([FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var resources = await db.Resources.AsNoTracking().Where(r => r.IsActive || (IsAdmin && includeInactive))
            .OrderBy(r => r.Name).ThenBy(r => r.Id).ToListAsync(cancellationToken);
        return Ok(resources.Select(ResourceResponse.From).ToArray());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ResourceResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public async Task<ActionResult<ResourceResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var resource = await db.Resources.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id && (r.IsActive || IsAdmin), cancellationToken);
        return resource is null ? Problem(statusCode: 404, title: "Resource not found.") : Ok(ResourceResponse.From(resource));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<ResourceResponse>(201, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<ActionResult<ResourceResponse>> Create(ResourceRequest request, CancellationToken cancellationToken)
    {
        var resource = new Resource();
        request.ApplyTo(resource);
        db.Resources.Add(resource);
        await db.SaveChangesAsync(cancellationToken);
        var response = ResourceResponse.From(resource);
        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, response);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<ResourceResponse>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<ActionResult<ResourceResponse>> Update(int id, ResourceRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var resource = await queries.LockResourceAsync(id, cancellationToken);
        if (resource is null)
            return Problem(statusCode: 404, title: "Resource not found.");
        // Configuration changes apply to future booking attempts; existing bookings are retained.
        request.ApplyTo(resource);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(ResourceResponse.From(resource));
    }
}
