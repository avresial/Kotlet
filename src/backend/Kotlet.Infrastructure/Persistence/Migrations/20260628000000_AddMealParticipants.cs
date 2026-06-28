using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMealParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "servings",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "meal_plan_item_participants",
                schema: "kotlet",
                columns: table => new
                {
                    meal_plan_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_item_participants", x => new { x.meal_plan_item_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_meal_plan_item_participants_meal_plan_items_meal_plan_item_id",
                        column: x => x.meal_plan_item_id,
                        principalSchema: "kotlet",
                        principalTable: "meal_plan_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_plan_item_participants_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "kotlet",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_item_participants_user_id",
                schema: "kotlet",
                table: "meal_plan_item_participants",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_plan_item_participants",
                schema: "kotlet");

            migrationBuilder.DropColumn(
                name: "servings",
                schema: "kotlet",
                table: "meal_plan_items");
        }
    }
}
