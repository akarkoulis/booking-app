using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingApp.Web.Api;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(BookingDbContext db, IPasswordHasher<User> passwordHasher) : BookingApiController
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> Get(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ThenBy(u => u.Id).ToListAsync(cancellationToken);
        return Ok(users.Select(UserResponse.From).ToArray());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<UserResponse>(200, "application/json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? Problem(statusCode: 404, title: "User not found.") : Ok(UserResponse.From(user));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<UserResponse>(201, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
            return DuplicateEmail();
        var user = new User { Email = request.Email, DisplayName = request.DisplayName, Role = request.Role!.Value, IsActive = true };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_Email" })
        {
            return DuplicateEmail();
        }
        var response = UserResponse.From(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<UserResponse>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    [ProducesResponseType<BookingProblemDetails>(422, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (id == CurrentUserId && (request.Role != UserRole.Admin || request.IsActive != true))
            return BookingProblemDetails.Result([new RuleViolation("cannot_change_own_access", "You cannot demote or deactivate yourself.")],
                title: "Cannot change your own access.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return Problem(statusCode: 404, title: "User not found.");
        user.DisplayName = request.DisplayName;
        user.Role = request.Role!.Value;
        user.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(UserResponse.From(user));
    }

    private ObjectResult DuplicateEmail() => Problem(statusCode: 409, title: "A user with this email already exists.");
}
