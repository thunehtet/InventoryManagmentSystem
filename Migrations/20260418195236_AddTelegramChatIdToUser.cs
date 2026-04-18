using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramChatIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "AspNetUsers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "AspNetUsers");
        }
        // Migration written manually — VS was locking the DLL during generation
    }
}
