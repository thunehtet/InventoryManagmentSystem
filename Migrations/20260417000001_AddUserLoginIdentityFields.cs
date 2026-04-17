using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoginIdentityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanChangeLoginIdentity",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginIdentityChangedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanChangeLoginIdentity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LoginIdentityChangedAt",
                table: "AspNetUsers");
        }
    }
}
