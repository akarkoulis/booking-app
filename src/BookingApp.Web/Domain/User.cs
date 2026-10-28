namespace BookingApp.Web.Domain;

public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = "";
}
