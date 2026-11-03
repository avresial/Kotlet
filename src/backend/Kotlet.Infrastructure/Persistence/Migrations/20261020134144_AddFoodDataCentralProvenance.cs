using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodDataCentralProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "food_data_central_id",
                schema: "kotlet",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "food_data_central_retrieved_at_utc",
                schema: "kotlet",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "food_data_central_id",
                schema: "kotlet",
                table: "products");

            migrationBuilder.DropColumn(
                name: "food_data_central_retrieved_at_utc",
                schema: "kotlet",
                table: "products");
        }
    }
}
