using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_plan_items",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_items", x => x.id);
                    table.CheckConstraint(
                        "CK_meal_plan_items_recipe_or_ingredient",
                        "(recipe_id IS NOT NULL AND ingredient_id IS NULL) OR (recipe_id IS NULL AND ingredient_id IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_items_user_date",
                schema: "kotlet",
                table: "meal_plan_items",
                columns: new[] { "user_id", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_items_user_date_slot",
                schema: "kotlet",
                table: "meal_plan_items",
                columns: new[] { "user_id", "planned_date", "slot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_plan_items",
                schema: "kotlet");
        }
    }
}
