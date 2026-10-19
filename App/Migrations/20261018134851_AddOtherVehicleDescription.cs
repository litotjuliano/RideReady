using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideReady.Migrations
{
    /// <inheritdoc />
    public partial class AddOtherVehicleDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtherVehicleDescription",
                table: "Bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherVehicleDescription",
                table: "Bookings");
        }
    }
}
