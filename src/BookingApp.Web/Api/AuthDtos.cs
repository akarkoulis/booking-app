using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Api;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "An email address is required."), StringLength(254, ErrorMessage = "The email address must be 254 characters or fewer.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "A password is required.")]
    public string Password { get; set; } = "";
}

public sealed record MeResponse(int Id, string Email, string DisplayName, UserRole Role, string OfficeTimeZone)
{
    public static MeResponse From(User user, IConfiguration configuration) => new(
        user.Id, user.Email, user.DisplayName, user.Role, configuration["Office:TimeZone"]!);
}
