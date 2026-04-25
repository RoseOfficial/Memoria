using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MemoriaServer.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MemoriaDbContext))]
    [Migration("20260423184051_AddIdentityAndClaimsFoundation")]
    public partial class AddIdentityAndClaimsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLodestoneCharacters");

            migrationBuilder.DropIndex(
                name: "IX_Users_GameAccountId",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "GameAccountId",
                table: "Users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "DiscordUserId",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuildMembershipCheckedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuildMember",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimVerifiedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClaimedByUserId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HideAlts",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideEncounters",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideEntirely",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountLinkCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationUserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLinkCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLinkCodes_Users_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PlayerLocalContentId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAttempts_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DiscordUserId",
                table: "Users",
                column: "DiscordUserId",
                unique: true,
                filter: "\"DiscordUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GameAccountId",
                table: "Users",
                column: "GameAccountId",
                unique: true,
                filter: "\"GameAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ClaimedByUserId",
                table: "Players",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_HideEntirely",
                table: "Players",
                column: "HideEntirely");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinkCodes_ApplicationUserId",
                table: "AccountLinkCodes",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLinkCodes_Code",
                table: "AccountLinkCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAttempts_ExpiresAt",
                table: "ClaimAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAttempts_PlayerLocalContentId",
                table: "ClaimAttempts",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAttempts_UserId_PlayerLocalContentId",
                table: "ClaimAttempts",
                columns: new[] { "UserId", "PlayerLocalContentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_ClaimedByUserId",
                table: "Players",
                column: "ClaimedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_ClaimedByUserId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "AccountLinkCodes");

            migrationBuilder.DropTable(
                name: "ClaimAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Users_DiscordUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GameAccountId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Players_ClaimedByUserId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_HideEntirely",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GuildMembershipCheckedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsGuildMember",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClaimVerifiedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HideAlts",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HideEncounters",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HideEntirely",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "GameAccountId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "UserLodestoneCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AvatarLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LodestoneId = table.Column<int>(type: "integer", nullable: false),
                    NameAndWorld = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLodestoneCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLodestoneCharacters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_GameAccountId",
                table: "Users",
                column: "GameAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLodestoneCharacters_LodestoneId",
                table: "UserLodestoneCharacters",
                column: "LodestoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLodestoneCharacters_UserId",
                table: "UserLodestoneCharacters",
                column: "UserId");
        }
    }
}
