using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeServings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "servings",
                schema: "kotlet",
                table: "recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "servings",
                schema: "kotlet",
                table: "recipes");
        }
    }
}
