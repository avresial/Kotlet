using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedMealsAndSharedImageStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recipe_image_sources_recipe_images_recipe_image_id",
                schema: "kotlet",
                table: "recipe_image_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_image_sources_sources_source_id",
                schema: "kotlet",
                table: "recipe_image_sources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plan_items_recipe_or_ingredient",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_recipe_image_sources",
                schema: "kotlet",
                table: "recipe_image_sources");

            migrationBuilder.CreateTable(
                name: "images",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_images", x => x.id));

            migrationBuilder.Sql("""
                INSERT INTO kotlet.images (id, file_name, content_type, file_size_bytes, content, alt_text, created_at_utc, updated_at_utc)
                SELECT id, file_name, content_type, file_size_bytes, content, alt_text, created_at_utc, updated_at_utc
                FROM kotlet.recipe_images;
                """);

            migrationBuilder.DropColumn(
                name: "alt_text",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "content",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "file_name",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.RenameTable(
                name: "recipe_image_sources",
                schema: "kotlet",
                newName: "image_sources",
                newSchema: "kotlet");

            migrationBuilder.RenameColumn(
                name: "recipe_image_id",
                schema: "kotlet",
                table: "image_sources",
                newName: "image_id");

            migrationBuilder.AddColumn<decimal>(
                name: "ingredient_quantity",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ingredient_unit",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_image_sources",
                schema: "kotlet",
                table: "image_sources",
                columns: new[] { "image_id", "source_id" });

            migrationBuilder.CreateTable(
                name: "prepared_meals",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    brand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    store = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    package_quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    package_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    servings = table.Column<int>(type: "integer", nullable: false),
                    calories_per_serving = table.Column<decimal>(type: "numeric", nullable: true),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    preparation_instructions = table.Column<string>(type: "text", nullable: true),
                    shopping_ingredient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepared_meals", x => x.id);
                    table.CheckConstraint("ck_prepared_meals_values", "servings > 0 AND (price IS NULL OR price >= 0) AND (calories_per_serving IS NULL OR calories_per_serving >= 0)");
                    table.ForeignKey(
                        name: "FK_prepared_meals_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prepared_meals_ingredients_shopping_ingredient_id",
                        column: x => x.shopping_ingredient_id,
                        principalSchema: "kotlet",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prepared_meal_addons",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_selected_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepared_meal_addons", x => x.id);
                    table.CheckConstraint("ck_prepared_meal_addons_quantity", "default_quantity > 0");
                    table.ForeignKey(
                        name: "FK_prepared_meal_addons_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalSchema: "kotlet",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prepared_meal_addons_prepared_meals_prepared_meal_id",
                        column: x => x.prepared_meal_id,
                        principalSchema: "kotlet",
                        principalTable: "prepared_meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prepared_meal_images",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepared_meal_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_prepared_meal_images_images_id",
                        column: x => x.id,
                        principalSchema: "kotlet",
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prepared_meal_images_prepared_meals_prepared_meal_id",
                        column: x => x.prepared_meal_id,
                        principalSchema: "kotlet",
                        principalTable: "prepared_meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_items_parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items",
                column: "parent_meal_plan_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_items_prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items",
                column: "prepared_meal_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items",
                sql: "(CASE WHEN recipe_id IS NULL THEN 0 ELSE 1 END + CASE WHEN ingredient_id IS NULL THEN 0 ELSE 1 END + CASE WHEN prepared_meal_id IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_prepared_meal_addons_ingredient_id",
                schema: "kotlet",
                table: "prepared_meal_addons",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_prepared_meal_addons_prepared_meal_id_ingredient_id",
                schema: "kotlet",
                table: "prepared_meal_addons",
                columns: new[] { "prepared_meal_id", "ingredient_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prepared_meal_images_prepared_meal_id_sort_order",
                schema: "kotlet",
                table: "prepared_meal_images",
                columns: new[] { "prepared_meal_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prepared_meals_house_id_name",
                schema: "kotlet",
                table: "prepared_meals",
                columns: new[] { "house_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_prepared_meals_shopping_ingredient_id",
                schema: "kotlet",
                table: "prepared_meals",
                column: "shopping_ingredient_id");

            migrationBuilder.AddForeignKey(
                name: "FK_image_sources_images_image_id",
                schema: "kotlet",
                table: "image_sources",
                column: "image_id",
                principalSchema: "kotlet",
                principalTable: "images",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_image_sources_sources_source_id",
                schema: "kotlet",
                table: "image_sources",
                column: "source_id",
                principalSchema: "kotlet",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plan_items_meal_plan_items_parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items",
                column: "parent_meal_plan_item_id",
                principalSchema: "kotlet",
                principalTable: "meal_plan_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plan_items_prepared_meals_prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items",
                column: "prepared_meal_id",
                principalSchema: "kotlet",
                principalTable: "prepared_meals",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_images_images_id",
                schema: "kotlet",
                table: "recipe_images",
                column: "id",
                principalSchema: "kotlet",
                principalTable: "images",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_image_sources_images_image_id",
                schema: "kotlet",
                table: "image_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_image_sources_sources_source_id",
                schema: "kotlet",
                table: "image_sources");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plan_items_meal_plan_items_parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plan_items_prepared_meals_prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_images_images_id",
                schema: "kotlet",
                table: "recipe_images");

            migrationBuilder.DropTable(
                name: "prepared_meal_addons",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "prepared_meal_images",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "prepared_meals",
                schema: "kotlet");

            migrationBuilder.DropIndex(
                name: "IX_meal_plan_items_parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropIndex(
                name: "IX_meal_plan_items_prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plan_items_one_source",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_image_sources",
                schema: "kotlet",
                table: "image_sources");

            migrationBuilder.DropColumn(
                name: "ingredient_quantity",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "ingredient_unit",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "parent_meal_plan_item_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.DropColumn(
                name: "prepared_meal_id",
                schema: "kotlet",
                table: "meal_plan_items");

            migrationBuilder.RenameTable(
                name: "image_sources",
                schema: "kotlet",
                newName: "recipe_image_sources",
                newSchema: "kotlet");

            migrationBuilder.RenameColumn(
                name: "image_id",
                schema: "kotlet",
                table: "recipe_image_sources",
                newName: "recipe_image_id");

            migrationBuilder.AddColumn<string>(
                name: "alt_text",
                schema: "kotlet",
                table: "recipe_images",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "content",
                schema: "kotlet",
                table: "recipe_images",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                schema: "kotlet",
                table: "recipe_images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                schema: "kotlet",
                table: "recipe_images",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                schema: "kotlet",
                table: "recipe_images",
                type: "character varying(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                schema: "kotlet",
                table: "recipe_images",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                schema: "kotlet",
                table: "recipe_images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE kotlet.recipe_images AS recipe
                SET file_name = image.file_name,
                    content_type = image.content_type,
                    file_size_bytes = image.file_size_bytes,
                    content = image.content,
                    alt_text = image.alt_text,
                    created_at_utc = image.created_at_utc,
                    updated_at_utc = image.updated_at_utc
                FROM kotlet.images AS image
                WHERE recipe.id = image.id;
                """);

            migrationBuilder.DropTable(
                name: "images",
                schema: "kotlet");

            migrationBuilder.AddPrimaryKey(
                name: "PK_recipe_image_sources",
                schema: "kotlet",
                table: "recipe_image_sources",
                columns: new[] { "recipe_image_id", "source_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plan_items_recipe_or_ingredient",
                schema: "kotlet",
                table: "meal_plan_items",
                sql: "(recipe_id IS NOT NULL AND ingredient_id IS NULL) OR (recipe_id IS NULL AND ingredient_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_image_sources_recipe_images_recipe_image_id",
                schema: "kotlet",
                table: "recipe_image_sources",
                column: "recipe_image_id",
                principalSchema: "kotlet",
                principalTable: "recipe_images",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_image_sources_sources_source_id",
                schema: "kotlet",
                table: "recipe_image_sources",
                column: "source_id",
                principalSchema: "kotlet",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
