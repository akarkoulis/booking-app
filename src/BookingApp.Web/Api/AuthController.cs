using System.Globalization;
using System.Security.Claims;
using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Api;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(BookingDbContext db, IPasswordHasher<User> passwordHasher, IConfiguration configuration) : ControllerBase
{
    private static readonly User DummyUser = new();
    private static readonly string DummyPasswordHash = new PasswordHasher<User>()
        .HashPassword(DummyUser, Guid.NewGuid().ToString());

    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<MeResponse>(200, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        // Always perform one verification, including when the email is unknown.
        var verification = passwordHasher.VerifyHashedPassword(user ?? DummyUser,
            user?.PasswordHash ?? DummyPasswordHash, request.Password);
        if (user is null || !user.IsActive || verification == PasswordVerificationResult.Failed)
            return Problem(statusCode: 401, title: "Invalid email or password.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Ok(MeResponse.From(user, configuration));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(415, "application/problem+json")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
