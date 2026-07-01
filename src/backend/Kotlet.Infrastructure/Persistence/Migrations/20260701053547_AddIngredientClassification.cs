using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "allergens",
                schema: "kotlet",
                table: "ingredients",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "attributes",
                schema: "kotlet",
                table: "ingredients",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "category",
                schema: "kotlet",
                table: "ingredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "suitability",
                schema: "kotlet",
                table: "ingredients",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allergens",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "attributes",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "suitability",
                schema: "kotlet",
                table: "ingredients");
        }
    }
}
