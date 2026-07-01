using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFoodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_food_settings",
                schema: "kotlet",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    avoided_allergens = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    avoided_attributes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    required_suitability = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_food_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_food_settings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "kotlet",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_excluded_ingredients",
                schema: "kotlet",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_excluded_ingredients", x => new { x.user_id, x.ingredient_id });
                    table.ForeignKey(
                        name: "FK_user_excluded_ingredients_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalSchema: "kotlet",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_excluded_ingredients_user_food_settings_user_id",
                        column: x => x.user_id,
                        principalSchema: "kotlet",
                        principalTable: "user_food_settings",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_excluded_ingredients_ingredient_id",
                schema: "kotlet",
                table: "user_excluded_ingredients",
                column: "ingredient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_excluded_ingredients",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "user_food_settings",
                schema: "kotlet");
        }
    }
}
