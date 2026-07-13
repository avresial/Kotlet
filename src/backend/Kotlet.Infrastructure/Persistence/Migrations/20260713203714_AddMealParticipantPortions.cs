using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealParticipantPortions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "portion_percent",
                schema: "kotlet",
                table: "meal_plan_item_participants",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plan_item_participants_portion_percent",
                schema: "kotlet",
                table: "meal_plan_item_participants",
                sql: "portion_percent BETWEEN 50 AND 150");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plan_item_participants_portion_percent",
                schema: "kotlet",
                table: "meal_plan_item_participants");

            migrationBuilder.DropColumn(
                name: "portion_percent",
                schema: "kotlet",
                table: "meal_plan_item_participants");
        }
    }
}
