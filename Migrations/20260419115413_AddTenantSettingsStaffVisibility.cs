using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsStaffVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LowStockAlertEnabled",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeCustomers",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeDashboard",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeFinance",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeSales",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffCanSeeTextiles",
                table: "TenantSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowStockAlertEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeCustomers",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeDashboard",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeFinance",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeSales",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StaffCanSeeTextiles",
                table: "TenantSettings");
        }
    }
}
