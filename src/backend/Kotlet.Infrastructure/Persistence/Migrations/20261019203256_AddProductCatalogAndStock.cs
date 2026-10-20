using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalogAndStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "products",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    package_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    package_quantity = table.Column<decimal>(type: "numeric(11,3)", precision: 11, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    calories_per100 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    protein_per100 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    carbs_per100 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    fat_per100 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    nutrition_basis = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_retrieved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("AK_products_house_id_id", x => new { x.house_id, x.id });
                    table.ForeignKey(
                        name: "FK_products_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_products_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalSchema: "kotlet",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_stock",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(11,3)", precision: 11, scale: 3, nullable: false),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    storage_location = table.Column<int>(type: "integer", nullable: true),
                    batch_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_stock", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_stock_products_house_id_product_id",
                        columns: x => new { x.house_id, x.product_id },
                        principalSchema: "kotlet",
                        principalTable: "products",
                        principalColumns: new[] { "house_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_stock_house_id_product_id",
                schema: "kotlet",
                table: "product_stock",
                columns: new[] { "house_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_stock_product_id_batch_key",
                schema: "kotlet",
                table: "product_stock",
                columns: new[] { "product_id", "batch_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_house_id_barcode",
                schema: "kotlet",
                table: "products",
                columns: new[] { "house_id", "barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_ingredient_id",
                schema: "kotlet",
                table: "products",
                column: "ingredient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_stock",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "products",
                schema: "kotlet");
        }
    }
}
