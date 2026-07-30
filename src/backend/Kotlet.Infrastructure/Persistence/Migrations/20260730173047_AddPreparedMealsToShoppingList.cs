using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedMealsToShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_shopping_list_items_house_ingredient",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                schema: "kotlet",
                table: "shopping_list_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items",
                column: "prepared_meal_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_list_items_house_ingredient",
                schema: "kotlet",
                table: "shopping_list_items",
                columns: new[] { "house_id", "ingredient_id" },
                unique: true,
                filter: "ingredient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_list_items_house_prepared_meal",
                schema: "kotlet",
                table: "shopping_list_items",
                columns: new[] { "house_id", "prepared_meal_id" },
                unique: true,
                filter: "prepared_meal_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items",
                sql: "(CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_shopping_list_items_prepared_meals_prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items",
                column: "prepared_meal_id",
                principalSchema: "kotlet",
                principalTable: "prepared_meals",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shopping_list_items_prepared_meals_prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropIndex(
                name: "IX_shopping_list_items_prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropIndex(
                name: "ux_shopping_list_items_house_ingredient",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropIndex(
                name: "ux_shopping_list_items_house_prepared_meal",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "prepared_meal_id",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "ingredient_id",
                schema: "kotlet",
                table: "shopping_list_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_shopping_list_items_house_ingredient",
                schema: "kotlet",
                table: "shopping_list_items",
                columns: new[] { "house_id", "ingredient_id" },
                unique: true);
        }
    }
}
