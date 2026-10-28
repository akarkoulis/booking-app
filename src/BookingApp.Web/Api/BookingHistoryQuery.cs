using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Web.Api;

public sealed class BookingHistoryQuery : IValidatableObject
{
    [FromQuery(Name = "resourceId"), Range(1, int.MaxValue)]
    public int? ResourceId { get; set; }
    [FromQuery(Name = "userId"), Range(1, int.MaxValue)]
    public int? UserId { get; set; }
    [FromQuery(Name = "from")]
    public string? From { get; set; }
    [FromQuery(Name = "to")]
    public string? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => OfficeDates.Validate(From, To);

    public BookingHistoryWindow ToWindow(OfficeTime officeTime) => BookingHistoryWindow.Create(officeTime, From, To);
}
