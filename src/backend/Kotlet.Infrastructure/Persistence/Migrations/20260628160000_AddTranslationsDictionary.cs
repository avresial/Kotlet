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
            // This migration is intentionally irreversible. Once the unique index on
            // ingredients.name is relaxed, duplicate names become valid data (most notably the
            // "Unknown" placeholder used for ingredients created in a non-default language).
            // Recreating the unique index during rollback would fail against that data, so a safe
            // automatic Down cannot be guaranteed without destructive de-duplication.
            throw new NotSupportedException(
                "Rolling back AddTranslationsDictionary is not supported: restoring the unique " +
                "ingredients.name index is unsafe once duplicate names (e.g. \"Unknown\") exist.");
        }
    }
}
