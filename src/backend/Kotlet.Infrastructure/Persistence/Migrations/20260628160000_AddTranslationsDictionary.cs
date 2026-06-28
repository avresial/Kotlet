using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationsDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ingredients_name",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.CreateTable(
                name: "translations",
                schema: "kotlet",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translations", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ingredients_name",
                schema: "kotlet",
                table: "ingredients",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translations",
                schema: "kotlet");

            migrationBuilder.DropIndex(
                name: "ix_ingredients_name",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.CreateIndex(
                name: "ux_ingredients_name",
                schema: "kotlet",
                table: "ingredients",
                column: "name",
                unique: true);
        }
    }
}
