using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdAtTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Textile",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Sales",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SaleItems",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductVariants",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Textile_TenantId",
                table: "Textile",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TenantId",
                table: "Sales",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_TenantId",
                table: "SaleItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_TenantId",
                table: "ProductVariants",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Tenants_TenantId",
                table: "ProductVariants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_Tenants_TenantId",
                table: "SaleItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Tenants_TenantId",
                table: "Sales",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Textile_Tenants_TenantId",
                table: "Textile",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Tenants_TenantId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_Tenants_TenantId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Tenants_TenantId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Textile_Tenants_TenantId",
                table: "Textile");

            migrationBuilder.DropIndex(
                name: "IX_Textile_TenantId",
                table: "Textile");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TenantId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_TenantId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_TenantId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Textile");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductVariants");
        }
    }
}
