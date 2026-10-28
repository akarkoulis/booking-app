using BookingApp.Web.Scheduling;
using BookingApp.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Route("health")]
public sealed class HealthController(
    BookingDbContext database,
    OfficeTime officeTime,
    IConfiguration configuration,
    ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
            {
                return Problem(statusCode: 503, title: "Database connection is not configured.");
            }

            // A real query checks connectivity without requiring tables or migrations.
            var result = await database.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (result != 1)
            {
                return Problem(statusCode: 503, title: "Database health check failed.");
            }

            var summer = officeTime.ToUtc(new DateTime(2026, 10, 20, 14, 0, 0));
            var winter = officeTime.ToUtc(new DateTime(2026, 10, 27, 14, 0, 0));
            if (summer != new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc)
                || winter != new DateTime(2026, 10, 27, 14, 0, 0, DateTimeKind.Utc))
            {
                return Problem(statusCode: 503, title: "Office time zone health check failed.");
            }

            return Ok(new HealthResponse("healthy", "ok", "ok"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not expose infrastructure details or connection strings to callers.
            logger.LogWarning("Health check failed ({ExceptionType}).", exception.GetType().Name);
            return Problem(statusCode: 503, title: "A required dependency is unavailable.");
        }
    }
}

public sealed record HealthResponse(string Status, string Database, string OfficeTimeZone);
