using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sources",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    author_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    retrieved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recipe_image_sources",
                schema: "kotlet",
                columns: table => new
                {
                    recipe_image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_image_sources", x => new { x.recipe_image_id, x.source_id });
                    table.ForeignKey(
                        name: "FK_recipe_image_sources_recipe_images_recipe_image_id",
                        column: x => x.recipe_image_id,
                        principalSchema: "kotlet",
                        principalTable: "recipe_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_image_sources_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "kotlet",
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_sources",
                schema: "kotlet",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_sources", x => new { x.recipe_id, x.source_id });
                    table.ForeignKey(
                        name: "FK_recipe_sources_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "kotlet",
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recipe_sources_sources_source_id",
                        column: x => x.source_id,
                        principalSchema: "kotlet",
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_image_sources_source_id",
                schema: "kotlet",
                table: "recipe_image_sources",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_sources_source_id",
                schema: "kotlet",
                table: "recipe_sources",
                column: "source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_image_sources",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "recipe_sources",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "sources",
                schema: "kotlet");
        }
    }
}
