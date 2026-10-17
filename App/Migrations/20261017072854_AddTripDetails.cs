using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideReady.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DropOffDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DropOffTime",
                table: "Bookings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromName",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToName",
                table: "Bookings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropOffDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DropOffTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FromName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ToName",
                table: "Bookings");
        }
    }
}
