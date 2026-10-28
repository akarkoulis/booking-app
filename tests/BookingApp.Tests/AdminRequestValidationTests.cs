using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;

namespace BookingApp.Tests;

public sealed class AdminRequestValidationTests
{
    [Theory]
    [InlineData("Name", "", "A name is required.")]
    [InlineData("Name", "long", "The name must be 100 characters or fewer.")]
    [InlineData("Type", null, "A type is required.")]
    [InlineData("Type", (ResourceType)99, "Choose a valid resource type.")]
    [InlineData("Location", "long", "The location must be 200 characters or fewer.")]
    [InlineData("Seats", 0, "Seats must be at least 1.")]
    [InlineData("Quantity", null, "A quantity is required.")]
    [InlineData("Quantity", 0, "The quantity must be at least 1.")]
    [InlineData("MinDurationMinutes", 0, "The minimum duration must be at least 1 minute.")]
    [InlineData("MaxDurationMinutes", 0, "The maximum duration must be at least 1 minute.")]
    [InlineData("MaxAdvanceDays", 0, "The maximum advance must be at least 1 day.")]
    [InlineData("MinNoticeMinutes", -1, "The minimum notice can't be negative.")]
    [InlineData("IsActive", null, "An active status is required.")]
    public void Resource_InvalidFieldHasExplicitMessage(string member, object? value, string message)
    {
        var request = ValidResource();
        if (value is "long")
            value = new string('x', member == "Name" ? 101 : 201);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(value, new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    [Theory]
    [InlineData("roomQuantity")]
    [InlineData("quantity")]
    [InlineData("opensOnly")]
    [InlineData("closesOnly")]
    [InlineData("equalHours")]
    [InlineData("reversedHours")]
    [InlineData("durationRange")]
    [InlineData("minDuration")]
    [InlineData("maxDuration")]
    [InlineData("advance")]
    [InlineData("notice")]
    [InlineData("seats")]
    [InlineData("name")]
    [InlineData("location")]
    [InlineData("type")]
    public void Resource_RejectsInvalidConstraints(string scenario)
    {
        var request = ValidResource();
        switch (scenario)
        {
            case "roomQuantity": request.Quantity = 2; break;
            case "quantity": request.Quantity = 0; break;
            case "opensOnly": request.OpensAt = new(9, 0); break;
            case "closesOnly": request.ClosesAt = new(17, 0); break;
            case "equalHours": request.OpensAt = request.ClosesAt = new(9, 0); break;
            case "reversedHours": request.OpensAt = new(17, 0); request.ClosesAt = new(9, 0); break;
            case "durationRange": request.MinDurationMinutes = 60; request.MaxDurationMinutes = 30; break;
            case "minDuration": request.MinDurationMinutes = 0; break;
            case "maxDuration": request.MaxDurationMinutes = 0; break;
            case "advance": request.MaxAdvanceDays = 0; break;
            case "notice": request.MinNoticeMinutes = -1; break;
            case "seats": request.Seats = 0; break;
            case "name": request.Name = new('x', 101); break;
            case "location": request.Location = new('x', 201); break;
            case "type": request.Type = (ResourceType)99; break;
        }
        var errors = Validate(request);
        Assert.NotEmpty(errors);
        var expectedMembers = scenario switch
        {
            "roomQuantity" => new[] { "quantity" },
            "opensOnly" or "closesOnly" or "equalHours" or "reversedHours" => ["opensAt", "closesAt"],
            "durationRange" => ["minDurationMinutes", "maxDurationMinutes"],
            _ => null
        };
        if (expectedMembers is not null)
            Assert.Equal(expectedMembers, errors.SelectMany(error => error.MemberNames));
        if (scenario is "opensOnly" or "closesOnly")
            Assert.Equal("Set both an opening and a closing time, or leave both empty.", Assert.Single(errors).ErrorMessage);
        if (scenario is "equalHours" or "reversedHours")
            Assert.Equal("The opening time must be before the closing time.", Assert.Single(errors).ErrorMessage);
    }

    [Theory]
    [InlineData(null, "Set both an opening and a closing time, or leave both empty.")]
    [InlineData("08:00", "The opening time must be before the closing time.")]
    public void Resource_OpeningHoursExplainMissingOrUnorderedTimes(string? closing, string message)
    {
        var request = ValidResource();
        request.OpensAt = new(9, 0);
        request.ClosesAt = closing is null ? null : TimeOnly.Parse(closing, CultureInfo.InvariantCulture);
        var error = Assert.Single(Validate(request));
        Assert.Equal(message, error.ErrorMessage);
        Assert.Equal(new[] { "opensAt", "closesAt" }, error.MemberNames);
    }

    [Theory]
    [InlineData("opensAt", "09:10")]
    [InlineData("closesAt", "09:10")]
    [InlineData("opensAt", "09:00:30")]
    [InlineData("closesAt", "09:00:30")]
    [InlineData("opensAt", "09:00:00.0000001")]
    [InlineData("closesAt", "09:00:00.0000001")]
    public void Resource_RejectsOpeningHoursOffGridWithFieldKey(string field, string value)
    {
        var request = ValidResource();
        request.OpensAt = new(8, 0);
        request.ClosesAt = new(18, 0);
        var time = TimeOnly.Parse(value, CultureInfo.InvariantCulture);
        if (field == "opensAt")
            request.OpensAt = time;
        else
            request.ClosesAt = time;

        var error = Assert.Single(Validate(request));
        Assert.Equal("Opening hours must be on a 15-minute boundary, for example 08:00 or 08:15.", error.ErrorMessage);
        Assert.Equal(field, Assert.Single(error.MemberNames));
    }

    [Theory]
    [InlineData("opensAt", "09:00")]
    [InlineData("closesAt", "09:00")]
    [InlineData("opensAt", "17:45")]
    [InlineData("closesAt", "17:45")]
    public void Resource_AcceptsGridOpeningHoursAndAssignsThemUnchanged(string field, string value)
    {
        var request = ValidResource();
        request.OpensAt = new(8, 0);
        request.ClosesAt = new(18, 0);
        var time = TimeOnly.Parse(value, CultureInfo.InvariantCulture);
        if (field == "opensAt")
            request.OpensAt = time;
        else
            request.ClosesAt = time;
        Assert.Empty(Validate(request));
        var resource = new Resource();
        request.ApplyTo(resource);
        Assert.Equal(request.OpensAt, resource.OpensAt);
        Assert.Equal(request.ClosesAt, resource.ClosesAt);
    }

    [Fact]
    public void Resource_AcceptsNullableLimitsZeroNoticeAndEqualDurationLimits()
    {
        var request = ValidResource();
        Assert.Empty(Validate(request));
        request.MinNoticeMinutes = 0;
        request.MinDurationMinutes = request.MaxDurationMinutes = 15;
        request.OpensAt = new(9, 0);
        request.ClosesAt = new(17, 0);
        Assert.Empty(Validate(request));
        Assert.NotEmpty(Validate(new ResourceRequest()));
    }

    [Theory]
    [InlineData("MaxActiveRoomBookingsPerUser", "The maximum active room bookings per user must be at least 1.")]
    [InlineData("DefaultMinNoticeMinutes", "The default minimum notice must be at least 1 minute.")]
    public void Policy_InvalidFieldHasExplicitMessage(string member, string message)
    {
        var request = new BookingPolicyRequest();
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(0, new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(-1, null, false)]
    public void Policy_RequiresPositiveLimitsWhenSet(int? roomLimit, int? notice, bool valid)
    {
        Assert.Equal(valid, Validate(new BookingPolicyRequest
        {
            MaxActiveRoomBookingsPerUser = roomLimit, DefaultMinNoticeMinutes = notice
        }).Count == 0);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-04-02", true)]
    [InlineData("2026-01-01", "2026-04-03", false)]
    [InlineData("2026-10-25", "2026-10-25", true)]
    [InlineData("2026-10-25", "2026-10-24", false)]
    [InlineData("2026-02-30", "2026-03-01", false)]
    [InlineData("2026-1-1", "2026-01-02", false)]
    [InlineData("9999-12-31", "9999-12-31", false)]
    [InlineData("", "2026-01-01", false)]
    public void Report_ValidatesInclusiveOfficeDateRange(string from, string to, bool valid)
    {
        var errors = Validate(new UtilisationQuery { From = from, To = to });
        Assert.Equal(valid, errors.Count == 0);
        if (!valid && from.Length != 0)
            Assert.All(errors.SelectMany(error => error.MemberNames), member => Assert.Contains(member, new[] { "from", "to" }));
    }

    [Theory]
    [InlineData(true, "Email", "", "An email address is required.")]
    [InlineData(true, "Email", "qa", "Enter a valid email address.")]
    [InlineData(true, "Email", "long", "The email address must be 254 characters or fewer.")]
    [InlineData(true, "DisplayName", "", "A display name is required.")]
    [InlineData(true, "DisplayName", "long", "The display name must be 100 characters or fewer.")]
    [InlineData(true, "Role", null, "A role is required.")]
    [InlineData(true, "Role", (UserRole)99, "Choose a valid role.")]
    [InlineData(true, "Password", "", "A password is required.")]
    [InlineData(true, "Password", "1234567", "The password must be at least 8 characters.")]
    [InlineData(false, "DisplayName", "", "A display name is required.")]
    [InlineData(false, "DisplayName", "long", "The display name must be 100 characters or fewer.")]
    [InlineData(false, "Role", null, "A role is required.")]
    [InlineData(false, "Role", (UserRole)99, "Choose a valid role.")]
    [InlineData(false, "IsActive", null, "An active status is required.")]
    public void User_InvalidFieldHasExplicitMessage(bool create, string member, object? value, string message)
    {
        object request = create ? new CreateUserRequest() : new UpdateUserRequest();
        if (value is "long")
            value = member == "Email" ? new string('x', 243) + "@example.com" : new string('x', 101);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(value, new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    [Fact]
    public void User_ValidatesPasswordLengthsAndRole()
    {
        var request = new CreateUserRequest { Email = "alice@example.com", DisplayName = "Alice", Role = UserRole.Employee, Password = "12345678" };
        Assert.Empty(Validate(request));
        request.Password = "1234567";
        Assert.NotEmpty(Validate(request));
        request.Password = "12345678";
        request.DisplayName = new('x', 101);
        Assert.NotEmpty(Validate(request));
        request.DisplayName = "Alice";
        request.Role = (UserRole)99;
        Assert.NotEmpty(Validate(request));
        Assert.NotEmpty(Validate(new UpdateUserRequest()));
    }

    [Fact]
    public void ProblemTitle_CanBeOverriddenWithoutChangingBookingDefaults()
    {
        RuleViolation[] violations = [new("cannot_change_own_access", "Keep your access.")];
        var custom = Assert.IsType<BookingProblemDetails>(BookingProblemDetails.Result(violations, title: "Cannot change your own access.").Value);
        Assert.Equal("Cannot change your own access.", custom.Title);
        Assert.Equal(422, custom.Status);
        var original = Assert.IsType<BookingProblemDetails>(BookingProblemDetails.Result(violations).Value);
        Assert.Equal("Booking rules were not satisfied.", original.Title);
    }

    [Theory]
    [InlineData("From", "A start date is required.")]
    [InlineData("To", "An end date is required.")]
    public void Report_RequiredDateHasExplicitMessage(string member, string message)
    {
        var request = new UtilisationQuery();
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty("", new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    [Theory]
    [InlineData("Email", "", "An email address is required.")]
    [InlineData("Email", "long", "The email address must be 254 characters or fewer.")]
    [InlineData("Password", "", "A password is required.")]
    public void Login_InvalidFieldHasExplicitMessage(string member, string value, string message)
    {
        var request = new LoginRequest();
        if (value == "long")
            value = new string('x', 255);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateProperty(value, new ValidationContext(request) { MemberName = member }, errors));
        Assert.Equal(message, Assert.Single(errors).ErrorMessage);
    }

    private static ResourceRequest ValidResource() => new() { Name = "Room", Type = ResourceType.Room, Quantity = 1, IsActive = true };

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}
