using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseKotletSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kotlet");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users",
                newSchema: "kotlet");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "refresh_tokens",
                newSchema: "kotlet");

            migrationBuilder.RenameTable(
                name: "pantry_items",
                newName: "pantry_items",
                newSchema: "kotlet");

            migrationBuilder.RenameTable(
                name: "ingredients",
                newName: "ingredients",
                newSchema: "kotlet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "users",
                schema: "kotlet",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "kotlet",
                newName: "refresh_tokens");

            migrationBuilder.RenameTable(
                name: "pantry_items",
                schema: "kotlet",
                newName: "pantry_items");

            migrationBuilder.RenameTable(
                name: "ingredients",
                schema: "kotlet",
                newName: "ingredients");
        }
    }
}
