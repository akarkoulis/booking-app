using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

public sealed class UtilisationQuery : IValidatableObject
{
    [FromQuery(Name = "from"), Required(ErrorMessage = "A start date is required.")]
    public string From { get; set; } = "";
    [FromQuery(Name = "to"), Required(ErrorMessage = "An end date is required.")]
    public string To { get; set; } = "";
    [FromQuery(Name = "type"), EnumDataType(typeof(ResourceType))]
    public ResourceType? Type { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => OfficeDates.Validate(From, To, required: true, maxDays: 92);

    public UtilisationWindow ToWindow(OfficeTime officeTime) => UtilisationWindow.Create(officeTime,
        OfficeDates.Parse(From), OfficeDates.Parse(To));
}

public sealed record UtilisationResponse(int ResourceId, string Name, ResourceType Type, bool IsActive, int Quantity,
    int BookingCount, decimal BookedHours, decimal AvailableHours, decimal? BookedUtilisation)
{
    public static UtilisationResponse From(Resource resource, UtilisationTotals totals) => new(resource.Id,
        resource.Name, resource.Type, resource.IsActive, resource.Quantity, totals.BookingCount,
        decimal.Round(totals.BookedTicks / TimeSpan.TicksPerHour, 2),
        decimal.Round(totals.AvailableTicks / TimeSpan.TicksPerHour, 2),
        totals.AvailableTicks == 0 ? null : decimal.Round(totals.BookedTicks / totals.AvailableTicks, 4));
}
