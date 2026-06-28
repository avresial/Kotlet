using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorIngredientMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "price",
                schema: "kotlet",
                table: "ingredients",
                newName: "price_per_100_base_units");

            migrationBuilder.RenameColumn(
                name: "calories_per_100_grams",
                schema: "kotlet",
                table: "ingredients",
                newName: "calories_per_100_base_units");

            migrationBuilder.AddColumn<Guid>(
                name: "ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "normalized_quantity",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_unit",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_countable",
                schema: "kotlet",
                table: "ingredients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "measurement_units_per_piece",
                schema: "kotlet",
                table: "ingredients",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE kotlet.ingredients
                SET measurement_unit = CASE
                    WHEN lower(trim(measurement_unit)) IN ('ml', 'l') THEN 'ml'
                    ELSE 'g'
                END;

                UPDATE kotlet.recipe_ingredients AS recipe_ingredient
                SET ingredient_id = ingredient.id,
                    normalized_quantity = COALESCE(recipe_ingredient.quantity, 1) * CASE
                        WHEN lower(trim(COALESCE(recipe_ingredient.unit, ingredient.measurement_unit))) IN ('kg', 'l') THEN 1000
                        WHEN lower(trim(COALESCE(recipe_ingredient.unit, ingredient.measurement_unit))) = 'cup' THEN 250
                        WHEN lower(trim(COALESCE(recipe_ingredient.unit, ingredient.measurement_unit))) = 'tbsp' THEN 15
                        WHEN lower(trim(COALESCE(recipe_ingredient.unit, ingredient.measurement_unit))) = 'tsp' THEN 5
                        ELSE 1
                    END,
                    normalized_unit = ingredient.measurement_unit
                FROM kotlet.ingredients AS ingredient
                WHERE lower(trim(ingredient.name)) = lower(trim(recipe_ingredient.name));
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "normalized_quantity",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_unit",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "name", schema: "kotlet", table: "recipe_ingredients");
            migrationBuilder.DropColumn(name: "quantity", schema: "kotlet", table: "recipe_ingredients");
            migrationBuilder.DropColumn(name: "unit", schema: "kotlet", table: "recipe_ingredients");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients",
                column: "ingredient_id");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_ingredients_ingredients_ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients",
                column: "ingredient_id",
                principalSchema: "kotlet",
                principalTable: "ingredients",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_countable",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "measurement_units_per_piece",
                schema: "kotlet",
                table: "ingredients");

            migrationBuilder.RenameColumn(
                name: "price_per_100_base_units",
                schema: "kotlet",
                table: "ingredients",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "calories_per_100_base_units",
                schema: "kotlet",
                table: "ingredients",
                newName: "calories_per_100_grams");

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE kotlet.recipe_ingredients AS recipe_ingredient
                SET name = ingredient.name,
                    quantity = recipe_ingredient.normalized_quantity,
                    unit = recipe_ingredient.normalized_unit
                FROM kotlet.ingredients AS ingredient
                WHERE ingredient.id = recipe_ingredient.ingredient_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "kotlet",
                table: "recipe_ingredients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_ingredients_ingredients_ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients");

            migrationBuilder.DropIndex(
                name: "ix_recipe_ingredients_ingredient_id",
                schema: "kotlet",
                table: "recipe_ingredients");

            migrationBuilder.DropColumn(name: "ingredient_id", schema: "kotlet", table: "recipe_ingredients");
            migrationBuilder.DropColumn(name: "normalized_quantity", schema: "kotlet", table: "recipe_ingredients");
            migrationBuilder.DropColumn(name: "normalized_unit", schema: "kotlet", table: "recipe_ingredients");
        }
    }
}
