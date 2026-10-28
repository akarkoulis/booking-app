using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Api;

public sealed class ResourceRequest : IValidatableObject
{
    [Required(ErrorMessage = "A name is required."), StringLength(100, ErrorMessage = "The name must be 100 characters or fewer.")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "A type is required."), EnumDataType(typeof(ResourceType), ErrorMessage = "Choose a valid resource type.")]
    public ResourceType? Type { get; set; }
    [StringLength(200, ErrorMessage = "The location must be 200 characters or fewer.")]
    public string? Location { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Seats must be at least 1.")]
    public int? Seats { get; set; }
    [Required(ErrorMessage = "A quantity is required."), Range(1, int.MaxValue, ErrorMessage = "The quantity must be at least 1.")]
    public int? Quantity { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The minimum duration must be at least 1 minute.")]
    public int? MinDurationMinutes { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The maximum duration must be at least 1 minute.")]
    public int? MaxDurationMinutes { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The maximum advance must be at least 1 day.")]
    public int? MaxAdvanceDays { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "The minimum notice can't be negative.")]
    public int? MinNoticeMinutes { get; set; }
    [Required(ErrorMessage = "An active status is required.")]
    public bool? IsActive { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == ResourceType.Room && Quantity != 1)
            yield return new("Room quantity must be 1.", ["quantity"]);
        const long quarterHourTicks = 15 * TimeSpan.TicksPerMinute;
        const string gridMessage = "Opening hours must be on a 15-minute boundary, for example 08:00 or 08:15.";
        if (OpensAt is { } opensAt && opensAt.Ticks % quarterHourTicks != 0)
            yield return new(gridMessage, ["opensAt"]);
        if (ClosesAt is { } closesAt && closesAt.Ticks % quarterHourTicks != 0)
            yield return new(gridMessage, ["closesAt"]);
        if (OpensAt.HasValue != ClosesAt.HasValue)
            yield return new("Set both an opening and a closing time, or leave both empty.", ["opensAt", "closesAt"]);
        else if (OpensAt.HasValue && OpensAt >= ClosesAt)
            yield return new("The opening time must be before the closing time.", ["opensAt", "closesAt"]);
        if (MinDurationMinutes.HasValue && MaxDurationMinutes.HasValue && MinDurationMinutes > MaxDurationMinutes)
            yield return new("Minimum duration must not exceed maximum duration.", ["minDurationMinutes", "maxDurationMinutes"]);
    }

    public void ApplyTo(Resource resource)
    {
        resource.Name = Name;
        resource.Type = Type!.Value;
        resource.Location = Location;
        resource.Seats = Seats;
        resource.Quantity = Quantity!.Value;
        resource.OpensAt = OpensAt;
        resource.ClosesAt = ClosesAt;
        resource.MinDurationMinutes = MinDurationMinutes;
        resource.MaxDurationMinutes = MaxDurationMinutes;
        resource.MaxAdvanceDays = MaxAdvanceDays;
        resource.MinNoticeMinutes = MinNoticeMinutes;
        resource.IsActive = IsActive!.Value;
    }
}

public sealed record ResourceResponse(int Id, string Name, ResourceType Type, string? Location, int? Seats,
    int Quantity, TimeOnly? OpensAt, TimeOnly? ClosesAt, int? MinDurationMinutes, int? MaxDurationMinutes,
    int? MaxAdvanceDays, int? MinNoticeMinutes, bool IsActive)
{
    public static ResourceResponse From(Resource resource) => new(resource.Id, resource.Name, resource.Type,
        resource.Location, resource.Seats, resource.Quantity, resource.OpensAt, resource.ClosesAt,
        resource.MinDurationMinutes, resource.MaxDurationMinutes, resource.MaxAdvanceDays,
        resource.MinNoticeMinutes, resource.IsActive);
}
