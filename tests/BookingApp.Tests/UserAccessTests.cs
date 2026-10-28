using System.Security.Claims;
using BookingApp.Web.Api;
using BookingApp.Web.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BookingApp.Tests;

public sealed class UserAccessTests
{
    [Theory]
    [InlineData(UserRole.Employee, true)]
    [InlineData(UserRole.Admin, false)]
    public async Task Update_RejectsChangingOwnAccessBeforeAnyDatabaseMutation(UserRole role, bool active)
    {
        var controller = new UsersController(null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "Admin")], "test"))
                }
            }
        };
        var result = await controller.Update(7, new UpdateUserRequest { DisplayName = "Admin", Role = role, IsActive = active }, default);
        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(422, response.StatusCode);
        var problem = Assert.IsType<BookingProblemDetails>(response.Value);
        Assert.Equal("Cannot change your own access.", problem.Title);
        Assert.Equal("cannot_change_own_access", Assert.Single(problem.Violations).Code);
    }
}
