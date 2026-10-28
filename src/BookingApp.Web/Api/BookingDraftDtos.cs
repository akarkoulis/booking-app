using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Ai;

namespace BookingApp.Web.Api;

public sealed class BookingDraftRequest
{
    [Required(ErrorMessage = "Enter a booking request.")]
    [StringLength(500, ErrorMessage = "The booking request must be 500 characters or fewer.")]
    public string? Text { get; set; }
}

public sealed record BookingDraftResponse : BookingDraftFields
{
    public AvailabilityResponse? Resource { get; init; }
    public required IReadOnlyList<AvailabilityResponse> Alternatives { get; init; }

    public static BookingDraftResponse From(BookingDraftFields fields, AvailabilityResponse? resource = null,
        IReadOnlyList<AvailabilityResponse>? alternatives = null) => new()
    {
        Date = fields.Date, StartTime = fields.StartTime, EndTime = fields.EndTime,
        ResourceType = fields.ResourceType, ResourceName = fields.ResourceName, Location = fields.Location,
        MinSeats = fields.MinSeats, Quantity = fields.Quantity, Title = fields.Title,
        NotUnderstood = fields.NotUnderstood, Resource = resource, Alternatives = alternatives ?? []
    };
}
