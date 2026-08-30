using System;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(KotletDbContext))]
    [Migration("20260830050000_AddCustomShoppingListItems")]
    public partial class AddCustomShoppingListItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_name",
                schema: "kotlet",
                table: "shopping_list_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.DropCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items",
                sql: "(CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END + CASE WHEN custom_name IS NULL THEN 0 ELSE 1 END) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "custom_name",
                schema: "kotlet",
                table: "shopping_list_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shopping_list_items_one_source",
                schema: "kotlet",
                table: "shopping_list_items",
                sql: "(CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1");
        }
    }
}
