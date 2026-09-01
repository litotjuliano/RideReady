using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add unique constraint for BookingReference
            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingReference",
                table: "Bookings",
                column: "BookingReference",
                unique: true);

            // Add unique constraint for Customer.Phone
            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true);

            // Add unique constraint for Driver.Phone
            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Phone",
                table: "Drivers",
                column: "Phone",
                unique: true);

            // Make BookingQuote.BookingId required (NOT NULL)
            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "BookingQuotes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove unique constraint for BookingReference
            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingReference",
                table: "Bookings");

            // Remove unique constraint for Customer.Phone
            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            // Remove unique constraint for Driver.Phone
            migrationBuilder.DropIndex(
                name: "IX_Drivers_Phone",
                table: "Drivers");

            // Make BookingQuote.BookingId nullable again
            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "BookingQuotes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false);
        }
    }
}
