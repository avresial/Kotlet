using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KotletDbContext))]
    [Migration("20260806222423_OptimizeRecipeQueryIndexes")]
    public partial class OptimizeRecipeQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_recipes_house_updated_id",
                schema: "kotlet",
                table: "recipes",
                columns: new[] { "house_id", "updated_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_recipes_house_created_id",
                schema: "kotlet",
                table: "recipes",
                columns: new[] { "house_id", "created_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recipes_house_created_id",
                schema: "kotlet",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "ix_recipes_house_updated_id",
                schema: "kotlet",
                table: "recipes");
        }
    }
}
