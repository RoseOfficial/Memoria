using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScannedPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserScannedPlayers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PlayerLocalContentId = table.Column<long>(type: "bigint", nullable: false),
                    LastScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScannedPlayers", x => new { x.UserId, x.PlayerLocalContentId });
                    table.ForeignKey(
                        name: "FK_UserScannedPlayers_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserScannedPlayers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserScannedPlayers_PlayerLocalContentId",
                table: "UserScannedPlayers",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserScannedPlayers_UserId_LastScannedAt",
                table: "UserScannedPlayers",
                columns: new[] { "UserId", "LastScannedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserScannedPlayers");
        }
    }
}
