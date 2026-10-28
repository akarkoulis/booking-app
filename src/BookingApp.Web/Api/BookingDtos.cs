using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;

namespace BookingApp.Web.Api;

public sealed class BookingRequest : IValidatableObject
{
    [Required(ErrorMessage = "Choose a resource.")]
    public int? ResourceId { get; set; }
    [Required(ErrorMessage = "A start date and time is required."), RegularExpression(OfficeTime.RequestPattern, ErrorMessage = "Enter a start date and time in the documented format."), Description(OfficeTime.RequestDescription)]
    public string Start { get; set; } = "";
    [Required(ErrorMessage = "An end date and time is required."), RegularExpression(OfficeTime.RequestPattern, ErrorMessage = "Enter an end date and time in the documented format."), Description(OfficeTime.RequestDescription)]
    public string End { get; set; } = "";
    [Required(ErrorMessage = "A quantity is required.")]
    public int? Quantity { get; set; }
    [StringLength(200, ErrorMessage = "The title must be 200 characters or fewer.")]
    public string? Title { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => RequestTimes.Validate(Start, End);
}

public sealed record BookingResponse(int Id, int ResourceId, string ResourceName, ResourceType ResourceType,
    int UserId, string UserDisplayName, string? Title, DateTimeOffset Start, DateTimeOffset End, int Quantity,
    BookingStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt)
{
    public static BookingResponse From(Booking booking, OfficeTime officeTime) => new(
        booking.Id, booking.ResourceId, booking.Resource.Name, booking.Resource.Type,
        booking.UserId, booking.User.DisplayName, booking.Title,
        officeTime.ToOfficeOffset(booking.StartUtc), officeTime.ToOfficeOffset(booking.EndUtc),
        booking.Quantity, booking.Status, officeTime.ToOfficeOffset(booking.CreatedAtUtc),
        booking.CancelledAtUtc is { } cancelled ? officeTime.ToOfficeOffset(cancelled) : null);
}
