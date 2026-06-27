using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pantry_items_users_user_id",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropIndex(
                name: "ux_pantry_items_user_ingredient",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "kotlet",
                table: "pantry_items",
                newName: "previous_user_id");

            migrationBuilder.CreateTable(
                name: "houses",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_houses", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "kotlet",
                table: "houses",
                columns: new[] { "id", "name" },
                values: new object[] { new Guid("8a8c2f75-5998-45e8-8888-1d03d5b45275"), "Default house" });

            migrationBuilder.AddColumn<Guid>(
                name: "house_id", schema: "kotlet", table: "users", type: "uuid", nullable: false,
                defaultValue: new Guid("8a8c2f75-5998-45e8-8888-1d03d5b45275"));

            migrationBuilder.AddColumn<Guid>(
                name: "house_id", schema: "kotlet", table: "pantry_items", type: "uuid", nullable: false,
                defaultValue: new Guid("8a8c2f75-5998-45e8-8888-1d03d5b45275"));

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY ingredient_id ORDER BY id) AS row_number,
                           SUM(quantity) OVER (PARTITION BY ingredient_id) AS total_quantity
                    FROM kotlet.pantry_items
                )
                UPDATE kotlet.pantry_items AS pantry
                SET quantity = ranked.total_quantity
                FROM ranked
                WHERE pantry.id = ranked.id AND ranked.row_number = 1;

                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY ingredient_id ORDER BY id) AS row_number
                    FROM kotlet.pantry_items
                )
                DELETE FROM kotlet.pantry_items AS pantry
                USING ranked
                WHERE pantry.id = ranked.id AND ranked.row_number > 1;
                """);

            migrationBuilder.DropColumn(name: "previous_user_id", schema: "kotlet", table: "pantry_items");

            migrationBuilder.CreateIndex(
                name: "ux_pantry_items_house_ingredient", schema: "kotlet", table: "pantry_items",
                columns: new[] { "house_id", "ingredient_id" }, unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_house_id",
                schema: "kotlet",
                table: "users",
                column: "house_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pantry_items_houses_house_id",
                schema: "kotlet",
                table: "pantry_items",
                column: "house_id",
                principalSchema: "kotlet",
                principalTable: "houses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_houses_house_id",
                schema: "kotlet",
                table: "users",
                column: "house_id",
                principalSchema: "kotlet",
                principalTable: "houses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pantry_items_houses_house_id",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropForeignKey(
                name: "FK_users_houses_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropIndex(name: "ux_pantry_items_house_ingredient", schema: "kotlet", table: "pantry_items");

            migrationBuilder.AddColumn<Guid>(name: "user_id", schema: "kotlet", table: "pantry_items", type: "uuid", nullable: true);

            migrationBuilder.Sql("""
                UPDATE kotlet.pantry_items AS pantry
                SET user_id = (
                    SELECT users.id FROM kotlet.users AS users
                    WHERE users.house_id = pantry.house_id
                    ORDER BY users.id LIMIT 1
                );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id", schema: "kotlet", table: "pantry_items", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.DropColumn(name: "house_id", schema: "kotlet", table: "pantry_items");
            migrationBuilder.DropColumn(name: "house_id", schema: "kotlet", table: "users");
            migrationBuilder.DropTable(name: "houses", schema: "kotlet");

            migrationBuilder.CreateIndex(
                name: "ux_pantry_items_user_ingredient", schema: "kotlet", table: "pantry_items",
                columns: new[] { "user_id", "ingredient_id" }, unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pantry_items_users_user_id",
                schema: "kotlet",
                table: "pantry_items",
                column: "user_id",
                principalSchema: "kotlet",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
