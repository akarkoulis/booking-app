using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Api;
using BookingApp.Web.Domain;

namespace BookingApp.Tests;

public class BookingRequestValidationTests
{
    [Theory]
    [InlineData(true, "Start", "", "A start date and time is required.")]
    [InlineData(true, "End", "", "An end date and time is required.")]
    [InlineData(true, "Start", "bad", "Enter a start date and time in the documented format.")]
    [InlineData(true, "End", "bad", "Enter an end date and time in the documented format.")]
    [InlineData(true, "MinSeats", 0, "Minimum seats must be at least 1.")]
    [InlineData(true, "Type", (ResourceType)99, "Choose a valid resource type.")]
    [InlineData(false, "ResourceId", null, "Choose a resource.")]
    [InlineData(false, "Quantity", null, "A quantity is required.")]
    [InlineData(false, "Start", "", "A start date and time is required.")]
    [InlineData(false, "End", "", "An end date and time is required.")]
    [InlineData(false, "Start", "bad", "Enter a start date and time in the documented format.")]
    [InlineData(false, "End", "bad", "Enter an end date and time in the documented format.")]
    public void InvalidField_HasExplicitMessage(bool availability, string member, object? value, string message)
    {
        object request = availability ? new AvailabilityQuery() : new BookingRequest();
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(value, new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    [Fact]
    public void OverlongTitle_HasExplicitMessage()
    {
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(new string('x', 201),
            new ValidationContext(new BookingRequest()) { MemberName = "Title" }, errors));
        Assert.Equal("The title must be 200 characters or fewer.", Assert.Single(errors).ErrorMessage);
    }
}
