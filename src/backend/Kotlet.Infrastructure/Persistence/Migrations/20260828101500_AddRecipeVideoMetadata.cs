using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KotletDbContext))]
    [Migration("20260828101500_AddRecipeVideoMetadata")]
    public partial class AddRecipeVideoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "video_thumbnail_url",
                schema: "kotlet",
                table: "recipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "video_url",
                schema: "kotlet",
                table: "recipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "video_thumbnail_url",
                schema: "kotlet",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "video_url",
                schema: "kotlet",
                table: "recipes");
        }
    }
}
