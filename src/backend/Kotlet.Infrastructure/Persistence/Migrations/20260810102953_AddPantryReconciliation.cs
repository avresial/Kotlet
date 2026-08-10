using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "conversion_confidence",
                schema: "kotlet",
                table: "pantry_items",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_observation_ids_json",
                schema: "kotlet",
                table: "pantry_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_observed_at_utc",
                schema: "kotlet",
                table: "pantry_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "last_observed_quantity",
                schema: "kotlet",
                table: "pantry_items",
                type: "numeric(11,3)",
                precision: 11,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_observed_unit",
                schema: "kotlet",
                table: "pantry_items",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "package_description",
                schema: "kotlet",
                table: "pantry_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pantry_version",
                schema: "kotlet",
                table: "houses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "pantry_reconciliation_operations",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pantry_version = table.Column<long>(type: "bigint", nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    undo_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    undo_state_json = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    undone_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    undo_response_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pantry_reconciliation_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_pantry_reconciliation_operations_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pantry_unmatched_phrases",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_phrase = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    normalized_phrase = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    candidate_ids_json = table.Column<string>(type: "text", nullable: false),
                    recognition_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    first_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pantry_unmatched_phrases", x => x.id);
                    table.ForeignKey(
                        name: "FK_pantry_unmatched_phrases_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pantry_reconciliation_operations_house_operation",
                schema: "kotlet",
                table: "pantry_reconciliation_operations",
                columns: new[] { "house_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pantry_reconciliation_operations_undo_token",
                schema: "kotlet",
                table: "pantry_reconciliation_operations",
                column: "undo_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pantry_unmatched_phrases_house_phrase_locale",
                schema: "kotlet",
                table: "pantry_unmatched_phrases",
                columns: new[] { "house_id", "normalized_phrase", "locale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pantry_reconciliation_operations",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "pantry_unmatched_phrases",
                schema: "kotlet");

            migrationBuilder.DropColumn(
                name: "conversion_confidence",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "last_observation_ids_json",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "last_observed_at_utc",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "last_observed_quantity",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "last_observed_unit",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "package_description",
                schema: "kotlet",
                table: "pantry_items");

            migrationBuilder.DropColumn(
                name: "pantry_version",
                schema: "kotlet",
                table: "houses");
        }
    }
}
