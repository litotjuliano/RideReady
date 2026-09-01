using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientContact",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Notifications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecipientContact",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Notifications");
        }
    }
}
