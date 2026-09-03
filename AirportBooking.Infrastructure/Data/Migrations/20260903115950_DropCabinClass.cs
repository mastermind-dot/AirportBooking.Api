using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirportBooking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropCabinClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CabinClass",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CabinClass",
                table: "Bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
