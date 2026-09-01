using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddLuggageFeeToProductSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LuggageFeePerExtra",
                table: "PricingSettings",
                type: "numeric",
                nullable: true,
                defaultValue: 5m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LuggageFeePerExtra",
                table: "PricingSettings");
        }
    }
}
