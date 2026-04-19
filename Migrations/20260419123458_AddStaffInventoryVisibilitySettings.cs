using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInventoryVisibilitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeInventory",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeProducts",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeStockMovement",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeVariants",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffCanSeeInventory",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeProducts",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeStockMovement",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeVariants",
                table: "TenantSettings");
        }
    }
}
