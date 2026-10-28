using Microsoft.EntityFrameworkCore;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Data;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<BookingPolicy> BookingPolicies => Set<BookingPolicy>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.Property(x => x.Email).HasMaxLength(254);
        user.Property(x => x.DisplayName).HasMaxLength(100);
        user.Property(x => x.PasswordHash).HasMaxLength(512);
        user.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        user.HasIndex(x => x.Email).IsUnique();
        user.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Users_Email_Lowercase", """ "Email" = lower("Email") """);
            t.HasCheckConstraint("CK_Users_Role", """ "Role" IN ('Admin', 'Employee') """);
        });

        var resource = modelBuilder.Entity<Resource>();
        resource.Property(x => x.Name).HasMaxLength(100);
        resource.Property(x => x.Location).HasMaxLength(200);
        resource.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        resource.Property(x => x.OpensAt).HasColumnType("time without time zone");
        resource.Property(x => x.ClosesAt).HasColumnType("time without time zone");
        resource.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Resources_Type", """ "Type" IN ('Room', 'Car', 'Equipment', 'Catering') """);
            t.HasCheckConstraint("CK_Resources_Quantity", """ "Quantity" >= 1 """);
            t.HasCheckConstraint("CK_Resources_RoomQuantity", """ "Type" <> 'Room' OR "Quantity" = 1 """);
            t.HasCheckConstraint("CK_Resources_Seats", """ "Seats" IS NULL OR "Seats" > 0 """);
            t.HasCheckConstraint("CK_Resources_OpeningHours", """ ("OpensAt" IS NULL AND "ClosesAt" IS NULL) OR ("OpensAt" IS NOT NULL AND "ClosesAt" IS NOT NULL AND "OpensAt" < "ClosesAt") """);
            t.HasCheckConstraint("CK_Resources_MinDurationMinutes", """ "MinDurationMinutes" IS NULL OR "MinDurationMinutes" > 0 """);
            t.HasCheckConstraint("CK_Resources_MaxDurationMinutes", """ "MaxDurationMinutes" IS NULL OR "MaxDurationMinutes" > 0 """);
            t.HasCheckConstraint("CK_Resources_DurationRange", """ "MinDurationMinutes" IS NULL OR "MaxDurationMinutes" IS NULL OR "MinDurationMinutes" <= "MaxDurationMinutes" """);
            t.HasCheckConstraint("CK_Resources_MaxAdvanceDays", """ "MaxAdvanceDays" IS NULL OR "MaxAdvanceDays" > 0 """);
            t.HasCheckConstraint("CK_Resources_MinNoticeMinutes", """ "MinNoticeMinutes" IS NULL OR "MinNoticeMinutes" >= 0 """);
        });

        var policy = modelBuilder.Entity<BookingPolicy>();
        policy.Property(x => x.Id).ValueGeneratedNever();
        policy.ToTable(t =>
        {
            t.HasCheckConstraint("CK_BookingPolicies_Singleton", """ "Id" = 1 """);
            t.HasCheckConstraint("CK_BookingPolicies_MaxActiveRoomBookingsPerUser", """ "MaxActiveRoomBookingsPerUser" IS NULL OR "MaxActiveRoomBookingsPerUser" > 0 """);
            t.HasCheckConstraint("CK_BookingPolicies_DefaultMinNoticeMinutes", """ "DefaultMinNoticeMinutes" IS NULL OR "DefaultMinNoticeMinutes" > 0 """);
        });

        var booking = modelBuilder.Entity<Booking>();
        booking.Property(x => x.Title).HasMaxLength(200);
        booking.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        booking.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        booking.HasOne(x => x.Resource).WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        booking.HasIndex(x => new { x.ResourceId, x.StartUtc });
        booking.HasIndex(x => new { x.ResourceId, x.StartUtc, x.EndUtc })
            .HasDatabaseName("IX_Bookings_ResourceId_StartUtc_EndUtc_Confirmed")
            .HasFilter(""" "Status" = 'Confirmed' """);
        booking.HasIndex(x => new { x.UserId, x.StartUtc });
        booking.HasIndex(x => new { x.UserId, x.EndUtc })
            .HasDatabaseName("IX_Bookings_UserId_EndUtc_Confirmed")
            .HasFilter(""" "Status" = 'Confirmed' """);
        booking.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Bookings_Interval", """ "StartUtc" < "EndUtc" """);
            t.HasCheckConstraint("CK_Bookings_Quantity", """ "Quantity" >= 1 """);
            t.HasCheckConstraint("CK_Bookings_Status", """ "Status" IN ('Confirmed', 'Cancelled') """);
            t.HasCheckConstraint("CK_Bookings_Cancellation", """ ("Status" = 'Cancelled') = ("CancelledAtUtc" IS NOT NULL) """);
        });
    }
}
