using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequirePreparedMealCalories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prepared_meals_values",
                schema: "kotlet",
                table: "prepared_meals");

            migrationBuilder.Sql("UPDATE kotlet.prepared_meals SET calories_per_serving = 0 WHERE calories_per_serving IS NULL");

            migrationBuilder.AlterColumn<decimal>(
                name: "calories_per_serving",
                schema: "kotlet",
                table: "prepared_meals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_prepared_meals_values",
                schema: "kotlet",
                table: "prepared_meals",
                sql: "servings > 0 AND (price IS NULL OR price >= 0) AND calories_per_serving >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_prepared_meals_values",
                schema: "kotlet",
                table: "prepared_meals");

            migrationBuilder.AlterColumn<decimal>(
                name: "calories_per_serving",
                schema: "kotlet",
                table: "prepared_meals",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddCheckConstraint(
                name: "ck_prepared_meals_values",
                schema: "kotlet",
                table: "prepared_meals",
                sql: "servings > 0 AND (price IS NULL OR price >= 0) AND (calories_per_serving IS NULL OR calories_per_serving >= 0)");
        }
    }
}
