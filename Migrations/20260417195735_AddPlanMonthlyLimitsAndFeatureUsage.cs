using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanMonthlyLimitsAndFeatureUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxMonthlyCustomerInvites",
                table: "Plans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMonthlyPdfInvoices",
                table: "Plans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMonthlyReceiptShares",
                table: "Plans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMonthlySales",
                table: "Plans",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantFeatureUsages",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    YearMonth = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Feature = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UsageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFeatureUsages", x => new { x.TenantId, x.YearMonth, x.Feature });
                    table.ForeignKey(
                        name: "FK_TenantFeatureUsages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantFeatureUsages");

            migrationBuilder.DropColumn(
                name: "MaxMonthlyCustomerInvites",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxMonthlyPdfInvoices",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxMonthlyReceiptShares",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxMonthlySales",
                table: "Plans");
        }
    }
}
