using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanMaxVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxVariants",
                table: "Plans",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxVariants",
                table: "Plans");
        }
    }
}
