using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirportBooking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharterRequestsAndAircraftType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AircraftType",
                table: "Flights",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CharterRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Company = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Origin = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Destination = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreferredAircraft = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PassengerCount = table.Column<int>(type: "integer", nullable: true),
                    CargoWeightKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CargoDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharterRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharterRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftType",
                table: "Flights",
                column: "AircraftType");

            migrationBuilder.CreateIndex(
                name: "IX_CharterRequests_ContactEmail",
                table: "CharterRequests",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_CharterRequests_Reference",
                table: "CharterRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharterRequests_Status_CreatedAtUtc",
                table: "CharterRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CharterRequests_UserId",
                table: "CharterRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharterRequests");

            migrationBuilder.DropIndex(
                name: "IX_Flights_AircraftType",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "AircraftType",
                table: "Flights");
        }
    }
}
