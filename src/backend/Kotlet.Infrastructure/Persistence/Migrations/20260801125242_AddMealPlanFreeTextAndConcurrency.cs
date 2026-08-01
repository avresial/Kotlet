using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanFreeTextAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.AddColumn<string>(
                name: "free_text",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_mutation_key",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items",
                sql: "(CASE WHEN recipe_id IS NULL THEN 0 ELSE 1 END + CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END + CASE WHEN free_text IS NULL THEN 0 ELSE 1 END) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "free_text",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "last_mutation_key",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items",
                sql: "(CASE WHEN recipe_id IS NULL THEN 0 ELSE 1 END + CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1");
        }
    }
}
