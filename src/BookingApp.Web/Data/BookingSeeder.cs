using BookingApp.Web.Scheduling;
using BookingApp.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookingApp.Web.Data;

public sealed class BookingSeeder(TimeProvider timeProvider, OfficeTime officeTime, IConfiguration configuration)
{
    public void Seed(BookingDbContext db)
    {
        if (db.Resources.Any())
        {
            return;
        }

        AddSeedData(db);
        db.SaveChanges();
    }

    public async Task SeedAsync(BookingDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Resources.AnyAsync(cancellationToken))
        {
            return;
        }

        AddSeedData(db);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void AddSeedData(BookingDbContext db)
    {
        // Read only when seeding is needed, so an already seeded database starts without these settings.
        var accounts = SeedAccounts.FromConfiguration(configuration);
        var data = BuildData(timeProvider.GetUtcNow().UtcDateTime, officeTime, accounts);
        db.Users.AddRange(data.Users);
        db.Resources.AddRange(data.Resources);
        db.BookingPolicies.Add(data.Policy);
        db.Bookings.AddRange(data.Bookings);
    }

    public static SeedData BuildData(DateTime now, OfficeTime officeTime, SeedAccounts accounts)
    {
        var today = officeTime.ToOfficeLocal(now).Date;

        // Demo account emails and passwords come from configuration. Only salted hashes are stored.
        var admin = CreateUser(accounts.AdminEmail, "Demo Administrator", UserRole.Admin, accounts.AdminPassword);
        var alice = CreateUser(accounts.AliceEmail, "Alice Morgan", UserRole.Employee, accounts.EmployeePassword);
        var ben = CreateUser(accounts.BenEmail, "Ben Taylor", UserRole.Employee, accounts.EmployeePassword);

        var thames = CreateResource("Thames", ResourceType.Room, "First floor", 1, 30, 240, 30, null);
        thames.Seats = 6;
        thames.OpensAt = new TimeOnly(8, 0);
        thames.ClosesAt = new TimeOnly(18, 0);
        var regent = CreateResource("Regent", ResourceType.Room, "Ground floor", 1, 30, 480, 30, 120);
        regent.Seats = 12;
        regent.OpensAt = new TimeOnly(8, 0);
        regent.ClosesAt = new TimeOnly(18, 0);
        var cars = CreateResource("Pool cars", ResourceType.Car, "Office car park", 3, 60, 1440, 30, 120);
        var projectors = CreateResource("Projectors", ResourceType.Equipment, "IT cupboard", 4, 15, 480, 30, null);
        var catering = CreateResource("Catering", ResourceType.Catering, "Office kitchen", 20, 15, 120, 14, 1440);
        catering.OpensAt = new TimeOnly(8, 0);
        catering.ClosesAt = new TimeOnly(17, 0);
        var policy = new BookingPolicy
        {
            Id = 1,
            MaxActiveRoomBookingsPerUser = 3,
            DefaultMinNoticeMinutes = 60
        };

        var pastRoomDay = Weekday(today.AddDays(-7), -1);
        var pastEquipmentDay = Weekday(today.AddDays(-6), -1);
        Booking[] bookings =
        [
            CreateBooking(officeTime, alice, thames, pastRoomDay, 10, 0, 60, 1, "Past team meeting",
                officeTime.ToUtc(pastRoomDay.AddDays(-2).AddHours(10))),
            CreateBooking(officeTime, ben, projectors, pastEquipmentDay, 14, 0, 60, 1, "Past presentation",
                officeTime.ToUtc(pastEquipmentDay.AddDays(-2).AddHours(10))),
            CreateBooking(officeTime, alice, thames, Weekday(today.AddDays(1), 1), 10, 0, 60, 1, "Team planning", now),
            CreateBooking(officeTime, ben, cars, Weekday(today.AddDays(4), 1), 9, 0, 180, 2, "Client visits", now),
            CreateBooking(officeTime, alice, projectors, Weekday(today.AddDays(7), 1), 10, 0, 60, 1, "Project presentation", now),
            CreateBooking(officeTime, alice, catering, Weekday(today.AddDays(10), 1), 12, 0, 30, 5, "Team lunch", now),
            CreateBooking(officeTime, ben, regent, Weekday(today.AddDays(12), -1), 14, 0, 60, 1, "Cancelled workshop", now, true)
        ];

        return new SeedData([admin, alice, ben], [thames, regent, cars, projectors, catering], policy, bookings);
    }

    private static User CreateUser(string email, string displayName, UserRole role, string password)
    {
        var user = new User { Email = email.Trim().ToLowerInvariant(), DisplayName = displayName, Role = role };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        return user;
    }

    private static Resource CreateResource(string name, ResourceType type, string location, int quantity,
        int minDuration, int maxDuration, int maxAdvanceDays, int? minNotice)
    {
        return new Resource
        {
            Name = name,
            Type = type,
            Location = location,
            Quantity = quantity,
            MinDurationMinutes = minDuration,
            MaxDurationMinutes = maxDuration,
            MaxAdvanceDays = maxAdvanceDays,
            MinNoticeMinutes = minNotice
        };
    }

    private static Booking CreateBooking(OfficeTime officeTime, User user, Resource resource, DateTime day, int hour, int minute,
        int durationMinutes, int quantity, string title, DateTime createdAtUtc, bool cancelled = false)
    {
        var start = day.AddHours(hour).AddMinutes(minute);
        return new Booking
        {
            User = user,
            Resource = resource,
            Title = title,
            StartUtc = officeTime.ToUtc(start),
            EndUtc = officeTime.ToUtc(start.AddMinutes(durationMinutes)),
            Quantity = quantity,
            CreatedAtUtc = createdAtUtc,
            Status = cancelled ? BookingStatus.Cancelled : BookingStatus.Confirmed,
            CancelledAtUtc = cancelled ? createdAtUtc : null
        };
    }

    private static DateTime Weekday(DateTime day, int direction)
    {
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(direction);
        }

        return day;
    }
}

public sealed record SeedData(IReadOnlyList<User> Users, IReadOnlyList<Resource> Resources,
    BookingPolicy Policy, IReadOnlyList<Booking> Bookings);

// Demo account settings: user-secrets locally, Seed__* environment variables when hosted.
public sealed record SeedAccounts(string AdminEmail, string AliceEmail, string BenEmail,
    string AdminPassword, string EmployeePassword)
{
    public const string AdminEmailKey = "Seed:AdminEmail";
    public const string AliceEmailKey = "Seed:AliceEmail";
    public const string BenEmailKey = "Seed:BenEmail";
    public const string AdminPasswordKey = "Seed:AdminPassword";
    public const string EmployeePasswordKey = "Seed:EmployeePassword";

    private static readonly string[] Keys =
        [AdminEmailKey, AliceEmailKey, BenEmailKey, AdminPasswordKey, EmployeePasswordKey];

    public static SeedAccounts FromConfiguration(IConfiguration configuration)
    {
        var missing = Keys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Demo accounts cannot be seeded. Set these configuration values: {string.Join(", ", missing)}.");
        }

        var accounts = new SeedAccounts(
            configuration[AdminEmailKey]!,
            configuration[AliceEmailKey]!,
            configuration[BenEmailKey]!,
            configuration[AdminPasswordKey]!,
            configuration[EmployeePasswordKey]!);

        // Emails are stored trimmed and lower-cased, so compare them the same way before the unique index does.
        var distinctEmails = new[] { accounts.AdminEmail, accounts.AliceEmail, accounts.BenEmail }
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct()
            .Count();
        if (distinctEmails != 3)
        {
            throw new InvalidOperationException(
                $"Demo accounts cannot be seeded. {AdminEmailKey}, {AliceEmailKey} and {BenEmailKey} must be different.");
        }

        return accounts;
    }

    // A record's generated ToString lists every property; keep the passwords out of logs and exception messages.
    public override string ToString() => nameof(SeedAccounts);
}