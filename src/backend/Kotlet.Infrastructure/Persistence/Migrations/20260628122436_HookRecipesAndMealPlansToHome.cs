using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HookRecipesAndMealPlansToHome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "house_id",
                schema: "kotlet",
                table: "recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "house_id",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "uuid",
                nullable: true);

            // Recipes and meal plans are now owned by a home. Backfill each row from its owner's
            // default home (falling back to any membership), then drop rows whose owner has no
            // home at all. Deleting recipes cascades their ingredients and images.
            migrationBuilder.Sql("""
                UPDATE kotlet.recipes SET house_id = COALESCE(
                    (SELECT u.default_house_id FROM kotlet.users AS u WHERE u.id = recipes.owner_user_id),
                    (SELECT m.house_id FROM kotlet.house_memberships AS m WHERE m.user_id = recipes.owner_user_id LIMIT 1));
                DELETE FROM kotlet.recipes WHERE house_id IS NULL;

                UPDATE kotlet.meal_plan_items SET house_id = COALESCE(
                    (SELECT u.default_house_id FROM kotlet.users AS u WHERE u.id = meal_plan_items.user_id),
                    (SELECT m.house_id FROM kotlet.house_memberships AS m WHERE m.user_id = meal_plan_items.user_id LIMIT 1));
                DELETE FROM kotlet.meal_plan_items WHERE house_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "house_id", schema: "kotlet", table: "recipes", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "house_id", schema: "kotlet", table: "meal_plan_items", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipes_house_id",
                schema: "kotlet",
                table: "recipes",
                column: "house_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_items_house_date",
                schema: "kotlet",
                table: "meal_plan_items",
                columns: new[] { "house_id", "planned_date" });

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plan_items_houses_house_id",
                schema: "kotlet",
                table: "meal_plan_items",
                column: "house_id",
                principalSchema: "kotlet",
                principalTable: "houses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recipes_houses_house_id",
                schema: "kotlet",
                table: "recipes",
                column: "house_id",
                principalSchema: "kotlet",
                principalTable: "houses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meal_plan_items_houses_house_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recipes_houses_house_id",
                schema: "kotlet",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "ix_recipes_house_id",
                schema: "kotlet",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "ix_meal_plan_items_house_date",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "house_id",
                schema: "kotlet",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "house_id",
                schema: "kotlet",
                table: "meal_plan_items");
        }
    }
}
