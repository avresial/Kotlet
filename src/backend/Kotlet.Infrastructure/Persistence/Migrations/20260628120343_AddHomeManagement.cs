using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kotlet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_house_id",
                schema: "kotlet",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "house_id",
                schema: "kotlet",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "house_invitations",
                schema: "kotlet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_house_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_house_invitations_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_house_invitations_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalSchema: "kotlet",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_house_invitations_users_invited_user_id",
                        column: x => x.invited_user_id,
                        principalSchema: "kotlet",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "house_memberships",
                schema: "kotlet",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_house_memberships", x => new { x.user_id, x.house_id });
                    table.ForeignKey(
                        name: "FK_house_memberships_houses_house_id",
                        column: x => x.house_id,
                        principalSchema: "kotlet",
                        principalTable: "houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_house_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "kotlet",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_default_house_id",
                schema: "kotlet",
                table: "users",
                column: "default_house_id");

            migrationBuilder.CreateIndex(
                name: "IX_house_invitations_invited_by_user_id",
                schema: "kotlet",
                table: "house_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_house_invitations_invited_user_id",
                schema: "kotlet",
                table: "house_invitations",
                column: "invited_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_house_invitations_house_user",
                schema: "kotlet",
                table: "house_invitations",
                columns: new[] { "house_id", "invited_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_house_memberships_house_id",
                schema: "kotlet",
                table: "house_memberships",
                column: "house_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_houses_default_house_id",
                schema: "kotlet",
                table: "users",
                column: "default_house_id",
                principalSchema: "kotlet",
                principalTable: "houses",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Backfill the new model from the single house_id every user used to carry: each user
            // becomes a member of that house, and it becomes their default. The previously seeded
            // "Default house" row is kept (it is now a regular house with those members).
            migrationBuilder.Sql("""
                INSERT INTO kotlet.house_memberships (user_id, house_id, joined_at_utc)
                SELECT id, house_id, now() FROM kotlet.users;

                UPDATE kotlet.users SET default_house_id = house_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_users_houses_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropColumn(
                name: "house_id",
                schema: "kotlet",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_houses_default_house_id",
                schema: "kotlet",
                table: "users");

            // Re-introduce the single house_id column and backfill it from the default home, falling back
            // to any membership, then to the legacy "Default house" for users with no home at all.
            migrationBuilder.AddColumn<Guid>(
                name: "house_id",
                schema: "kotlet",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO kotlet.houses (id, name)
                VALUES ('8a8c2f75-5998-45e8-8888-1d03d5b45275', 'Default house')
                ON CONFLICT (id) DO NOTHING;

                UPDATE kotlet.users SET house_id = COALESCE(
                    default_house_id,
                    (SELECT m.house_id FROM kotlet.house_memberships AS m WHERE m.user_id = users.id LIMIT 1),
                    '8a8c2f75-5998-45e8-8888-1d03d5b45275');
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "house_id",
                schema: "kotlet",
                table: "users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "house_invitations",
                schema: "kotlet");

            migrationBuilder.DropTable(
                name: "house_memberships",
                schema: "kotlet");

            migrationBuilder.DropIndex(
                name: "ix_users_default_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropColumn(
                name: "default_house_id",
                schema: "kotlet",
                table: "users");

            migrationBuilder.DropColumn(
                name: "house_id",
                schema: "kotlet",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_users_house_id",
                schema: "kotlet",
                table: "users",
                column: "house_id");

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
    }
}
