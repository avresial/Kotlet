using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalysisJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "video_analysis_jobs",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_reason = table.Column<string>(type: "text", nullable: true),
                    transcript = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    detected_ingredients_json = table.Column<string>(type: "text", nullable: true),
                    draft_json = table.Column<string>(type: "text", nullable: true),
                    recipe_import_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_analysis_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_video_analysis_jobs_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_video_analysis_jobs_house_id",
                schema: "kotlet",
                table: "video_analysis_jobs",
                column: "house_id");

            migrationBuilder.CreateIndex(
                name: "ix_video_analysis_jobs_user_id",
                schema: "kotlet",
                table: "video_analysis_jobs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "video_analysis_jobs",
                schema: "kotlet");
        }
    }
}
