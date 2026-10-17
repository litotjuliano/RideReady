using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideReady.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPassengerCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassengerCapacity",
                table: "Drivers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassengerCapacity",
                table: "Drivers");
        }
    }
}
