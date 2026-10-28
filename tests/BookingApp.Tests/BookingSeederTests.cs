using BookingApp.Web.Data;
using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace BookingApp.Tests;

public class BookingSeederTests
{
    private static readonly SeedAccounts TestAccounts = new(
        "admin@test.invalid", "alice@test.invalid", "ben@test.invalid", "admin-test-password", "employee-test-password");

    [Theory]
    [InlineData(19, 8)]
    [InlineData(23, 17)]
    [InlineData(24, 12)]
    public void EveryConfirmedFutureSeedBooking_PassesAllRulesAtSeveralSeedInstants(int day, int hour)
    {
        var now = RuleTestData.OfficeTime.ToUtc(new DateTime(2026, 10, day, hour, 0, 0));
        var data = BookingSeeder.BuildData(now, RuleTestData.OfficeTime, TestAccounts);
        var future = data.Bookings.Where(b => b.Status == BookingStatus.Confirmed && b.StartUtc > now).ToArray();
        Assert.NotEmpty(future);

        foreach (var booking in future)
        {
            var others = data.Bookings.Where(b => !ReferenceEquals(b, booking) && b.Status == BookingStatus.Confirmed);
            var overlap = others.Where(b => ReferenceEquals(b.Resource, booking.Resource)
                && b.StartUtc < booking.EndUtc && booking.StartUtc < b.EndUtc).Sum(b => (long)b.Quantity);
            var activeRooms = others.Count(b => ReferenceEquals(b.User, booking.User)
                && b.Resource.Type == ResourceType.Room && b.EndUtc > now);
            var context = RuleContextFactory.Create(RuleTestData.OfficeTime, booking.StartUtc, booking.EndUtc,
                booking.Quantity, booking.Resource, booking.User, data.Policy, now, overlap, activeRooms);

            Assert.All(BookingRules.All, rule => Assert.Null(rule.Evaluate(context)));
        }
    }

    [Fact]
    public void BuildData_StoresConfiguredAccountsWithNormalisedEmailsAndHashedPasswords()
    {
        var accounts = TestAccounts with { AdminEmail = "  Admin@Test.Invalid " };
        var data = BookingSeeder.BuildData(RuleTestData.Utc(12), RuleTestData.OfficeTime, accounts);

        Assert.Equal(new[] { "admin@test.invalid", "alice@test.invalid", "ben@test.invalid" }, data.Users.Select(u => u.Email));
        Assert.Equal(new[] { UserRole.Admin, UserRole.Employee, UserRole.Employee }, data.Users.Select(u => u.Role));

        var hasher = new PasswordHasher<User>();
        var passwords = new[] { "admin-test-password", "employee-test-password", "employee-test-password" };
        foreach (var (user, password) in data.Users.Zip(passwords))
        {
            Assert.NotEqual(password, user.PasswordHash);
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, password));
        }
    }

    [Fact]
    public void FromConfiguration_ReadsAllFiveValuesAndToStringHidesThem()
    {
        var accounts = SeedAccounts.FromConfiguration(Configuration(CompleteValues()));

        Assert.Equal(TestAccounts, accounts);
        Assert.Equal("SeedAccounts", accounts.ToString());
    }

    [Fact]
    public void FromConfiguration_ListsEveryMissingOrBlankKey()
    {
        var configuration = Configuration(new()
        {
            [SeedAccounts.AdminEmailKey] = "admin@test.invalid",
            [SeedAccounts.AliceEmailKey] = "   "
        });

        var error = Assert.Throws<InvalidOperationException>(() => SeedAccounts.FromConfiguration(configuration));

        Assert.Equal("Demo accounts cannot be seeded. Set these configuration values: "
            + "Seed:AliceEmail, Seed:BenEmail, Seed:AdminPassword, Seed:EmployeePassword.", error.Message);
    }

    [Fact]
    public void FromConfiguration_RejectsEmailsThatMatchAfterNormalisation()
    {
        var values = CompleteValues();
        values[SeedAccounts.BenEmailKey] = " ALICE@test.invalid ";

        var error = Assert.Throws<InvalidOperationException>(() => SeedAccounts.FromConfiguration(Configuration(values)));

        Assert.Equal("Demo accounts cannot be seeded. "
            + "Seed:AdminEmail, Seed:AliceEmail and Seed:BenEmail must be different.", error.Message);
    }

    private static Dictionary<string, string?> CompleteValues() => new()
    {
        [SeedAccounts.AdminEmailKey] = TestAccounts.AdminEmail,
        [SeedAccounts.AliceEmailKey] = TestAccounts.AliceEmail,
        [SeedAccounts.BenEmailKey] = TestAccounts.BenEmail,
        [SeedAccounts.AdminPasswordKey] = TestAccounts.AdminPassword,
        [SeedAccounts.EmployeePasswordKey] = TestAccounts.EmployeePassword
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}