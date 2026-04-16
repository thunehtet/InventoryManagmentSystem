using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class HardenSaleHistoryAndRestrictInventoryDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_ProductVariants_ProductVariantId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductVariants_ProductVariantId",
                table: "StockMovements");

            migrationBuilder.AddColumn<string>(
                name: "ProductColorSnapshot",
                table: "SaleItems",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "SaleItems",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductSizeSnapshot",
                table: "SaleItems",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductSkuSnapshot",
                table: "SaleItems",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE SaleItems si
                INNER JOIN ProductVariants pv ON pv.Id = si.ProductVariantId
                INNER JOIN Products p ON p.Id = pv.ProductId
                SET
                    si.ProductNameSnapshot = p.Name,
                    si.ProductSkuSnapshot = pv.SKU,
                    si.ProductColorSnapshot = pv.Color,
                    si.ProductSizeSnapshot = pv.Size
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_ProductVariants_ProductVariantId",
                table: "SaleItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductVariants_ProductVariantId",
                table: "StockMovements",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_ProductVariants_ProductVariantId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductVariants_ProductVariantId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ProductColorSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductSizeSnapshot",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductSkuSnapshot",
                table: "SaleItems");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_ProductVariants_ProductVariantId",
                table: "SaleItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductVariants_ProductVariantId",
                table: "StockMovements",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
