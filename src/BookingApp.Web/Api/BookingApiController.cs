using System.Globalization;
using System.Security.Claims;
using BookingApp.Web.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

public abstract class BookingApiController : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    protected bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));
}
