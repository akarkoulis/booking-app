using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Api;

public sealed class CreateUserRequest
{
    private string email = "";
    [Required(ErrorMessage = "An email address is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254, ErrorMessage = "The email address must be 254 characters or fewer.")]
    public string Email { get => email; set => email = value?.Trim().ToLowerInvariant() ?? ""; }
    [Required(ErrorMessage = "A display name is required."), StringLength(100, ErrorMessage = "The display name must be 100 characters or fewer.")]
    public string DisplayName { get; set; } = "";
    [Required(ErrorMessage = "A role is required."), EnumDataType(typeof(UserRole), ErrorMessage = "Choose a valid role.")]
    public UserRole? Role { get; set; }
    [Required(ErrorMessage = "A password is required."), MinLength(8, ErrorMessage = "The password must be at least 8 characters.")]
    public string Password { get; set; } = "";
}

public sealed class UpdateUserRequest
{
    [Required(ErrorMessage = "A display name is required."), StringLength(100, ErrorMessage = "The display name must be 100 characters or fewer.")]
    public string DisplayName { get; set; } = "";
    [Required(ErrorMessage = "A role is required."), EnumDataType(typeof(UserRole), ErrorMessage = "Choose a valid role.")]
    public UserRole? Role { get; set; }
    [Required(ErrorMessage = "An active status is required.")]
    public bool? IsActive { get; set; }
}

public sealed record UserResponse(int Id, string Email, string DisplayName, UserRole Role, bool IsActive)
{
    public static UserResponse From(User user) => new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive);
}
