using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add column only if it doesn't already exist (handles partial migration re-run)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tblname = 'Sales';
                SET @colname = 'InvoiceToken';
                SET @exists = (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                      AND TABLE_NAME  = @tblname
                      AND COLUMN_NAME = @colname
                );
                SET @sql = IF(@exists = 0,
                    'ALTER TABLE `Sales` ADD `InvoiceToken` char(36) NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000''',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Backfill each existing row with its own unique UUID
            migrationBuilder.Sql(@"
                UPDATE Sales
                SET InvoiceToken = UUID()
                WHERE InvoiceToken = '00000000-0000-0000-0000-000000000000';
            ");

            // Drop the index if it was partially created, then recreate
            migrationBuilder.Sql(@"
                SET @idx_exists = (
                    SELECT COUNT(*) FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME  = 'Sales'
                      AND INDEX_NAME  = 'IX_Sales_InvoiceToken'
                );
                SET @drop_sql = IF(@idx_exists > 0,
                    'DROP INDEX `IX_Sales_InvoiceToken` ON `Sales`',
                    'SELECT 1'
                );
                PREPARE drop_stmt FROM @drop_sql;
                EXECUTE drop_stmt;
                DEALLOCATE PREPARE drop_stmt;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceToken",
                table: "Sales",
                column: "InvoiceToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceToken",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceToken",
                table: "Sales");
        }
    }
}
