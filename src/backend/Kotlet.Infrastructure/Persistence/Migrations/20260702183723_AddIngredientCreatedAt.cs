using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                schema: "kotlet",
                table: "ingredients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at_utc",
                schema: "kotlet",
                table: "ingredients");
        }
    }
}
