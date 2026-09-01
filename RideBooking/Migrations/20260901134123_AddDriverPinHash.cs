using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPinHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: PricingSettings.LuggageFeePerExtra is intentionally NOT added here.
            // That column was already added by the earlier AddLuggageFeeToProductSetting
            // migration; the model snapshot had simply drifted out of sync with it. This
            // migration only fixes the snapshot for that column (see
            // RideBookingDbContextModelSnapshot.cs) without re-issuing its AddColumn, to
            // avoid a duplicate-column error on databases where that migration already ran.
            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Drivers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "Drivers");
        }
    }
}
