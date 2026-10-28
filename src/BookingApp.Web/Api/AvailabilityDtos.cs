using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

public sealed class AvailabilityQuery : IValidatableObject
{
    [FromQuery(Name = "start"), Required(ErrorMessage = "A start date and time is required."), RegularExpression(OfficeTime.RequestPattern, ErrorMessage = "Enter a start date and time in the documented format."), Description(OfficeTime.RequestDescription)]
    public string Start { get; set; } = "";
    [FromQuery(Name = "end"), Required(ErrorMessage = "An end date and time is required."), RegularExpression(OfficeTime.RequestPattern, ErrorMessage = "Enter an end date and time in the documented format."), Description(OfficeTime.RequestDescription)]
    public string End { get; set; } = "";
    [FromQuery(Name = "quantity"), Description("Number of units requested; defaults to 1.")]
    public int Quantity { get; set; } = 1;
    [FromQuery(Name = "type"), EnumDataType(typeof(ResourceType), ErrorMessage = "Choose a valid resource type.")]
    public ResourceType? Type { get; set; }
    [FromQuery(Name = "minSeats"), Range(1, int.MaxValue, ErrorMessage = "Minimum seats must be at least 1.")]
    public int? MinSeats { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => RequestTimes.Validate(Start, End);
}

public sealed record AvailabilityResponse(int ResourceId, string ResourceName, ResourceType ResourceType,
    string? Location, int? Seats, int Quantity, bool Available, IReadOnlyList<RuleViolation> Violations,
    DateTimeOffset Start, DateTimeOffset End)
{
    public static AvailabilityResponse From(Resource resource, IReadOnlyList<RuleViolation> violations,
        DateTime startUtc, DateTime endUtc, OfficeTime officeTime) => new(resource.Id, resource.Name, resource.Type,
            resource.Location, resource.Seats, resource.Quantity, violations.Count == 0, violations,
            officeTime.ToOfficeOffset(startUtc), officeTime.ToOfficeOffset(endUtc));
}
