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
    [Migration("20260424005610_AddTakedownRequests")]
    public partial class AddTakedownRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TakedownRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorldSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResolvedPlayerLocalContentId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmitterIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TakedownRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TakedownRequests_Players_ResolvedPlayerLocalContentId",
                        column: x => x.ResolvedPlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TakedownRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_ResolvedByUserId",
                table: "TakedownRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_ResolvedPlayerLocalContentId",
                table: "TakedownRequests",
                column: "ResolvedPlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_Status",
                table: "TakedownRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_SubmittedAt",
                table: "TakedownRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_SubmitterIpHash",
                table: "TakedownRequests",
                column: "SubmitterIpHash");

            migrationBuilder.CreateIndex(
                name: "IX_TakedownRequests_WorldSlug_NameSlug",
                table: "TakedownRequests",
                columns: new[] { "WorldSlug", "NameSlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TakedownRequests");
        }
    }
}
