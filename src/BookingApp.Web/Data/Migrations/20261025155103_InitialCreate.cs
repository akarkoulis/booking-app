using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingApp.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveRoomBookingsPerUser = table.Column<int>(type: "integer", nullable: true),
                    DefaultMinNoticeMinutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPolicies", x => x.Id);
                    table.CheckConstraint("CK_BookingPolicies_DefaultMinNoticeMinutes", " \"DefaultMinNoticeMinutes\" IS NULL OR \"DefaultMinNoticeMinutes\" > 0 ");
                    table.CheckConstraint("CK_BookingPolicies_MaxActiveRoomBookingsPerUser", " \"MaxActiveRoomBookingsPerUser\" IS NULL OR \"MaxActiveRoomBookingsPerUser\" > 0 ");
                    table.CheckConstraint("CK_BookingPolicies_Singleton", " \"Id\" = 1 ");
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Seats = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MinDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxAdvanceDays = table.Column<int>(type: "integer", nullable: true),
                    MinNoticeMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.CheckConstraint("CK_Resources_DurationRange", " \"MinDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" IS NULL OR \"MinDurationMinutes\" <= \"MaxDurationMinutes\" ");
                    table.CheckConstraint("CK_Resources_MaxAdvanceDays", " \"MaxAdvanceDays\" IS NULL OR \"MaxAdvanceDays\" > 0 ");
                    table.CheckConstraint("CK_Resources_MaxDurationMinutes", " \"MaxDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" > 0 ");
                    table.CheckConstraint("CK_Resources_MinDurationMinutes", " \"MinDurationMinutes\" IS NULL OR \"MinDurationMinutes\" > 0 ");
                    table.CheckConstraint("CK_Resources_MinNoticeMinutes", " \"MinNoticeMinutes\" IS NULL OR \"MinNoticeMinutes\" >= 0 ");
                    table.CheckConstraint("CK_Resources_OpeningHours", " (\"OpensAt\" IS NULL AND \"ClosesAt\" IS NULL) OR (\"OpensAt\" IS NOT NULL AND \"ClosesAt\" IS NOT NULL AND \"OpensAt\" < \"ClosesAt\") ");
                    table.CheckConstraint("CK_Resources_Quantity", " \"Quantity\" >= 1 ");
                    table.CheckConstraint("CK_Resources_RoomQuantity", " \"Type\" <> 'Room' OR \"Quantity\" = 1 ");
                    table.CheckConstraint("CK_Resources_Seats", " \"Seats\" IS NULL OR \"Seats\" > 0 ");
                    table.CheckConstraint("CK_Resources_Type", " \"Type\" IN ('Room', 'Car', 'Equipment', 'Catering') ");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Email_Lowercase", " \"Email\" = lower(\"Email\") ");
                    table.CheckConstraint("CK_Users_Role", " \"Role\" IN ('Admin', 'Employee') ");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResourceId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_Cancellation", " (\"Status\" = 'Cancelled') = (\"CancelledAtUtc\" IS NOT NULL) ");
                    table.CheckConstraint("CK_Bookings_Interval", " \"StartUtc\" < \"EndUtc\" ");
                    table.CheckConstraint("CK_Bookings_Quantity", " \"Quantity\" >= 1 ");
                    table.CheckConstraint("CK_Bookings_Status", " \"Status\" IN ('Confirmed', 'Cancelled') ");
                    table.ForeignKey(
                        name: "FK_Bookings_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId_StartUtc",
                table: "Bookings",
                columns: new[] { "ResourceId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId_StartUtc_EndUtc_Confirmed",
                table: "Bookings",
                columns: new[] { "ResourceId", "StartUtc", "EndUtc" },
                filter: " \"Status\" = 'Confirmed' ");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_EndUtc_Confirmed",
                table: "Bookings",
                columns: new[] { "UserId", "EndUtc" },
                filter: " \"Status\" = 'Confirmed' ");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId_StartUtc",
                table: "Bookings",
                columns: new[] { "UserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingPolicies");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
