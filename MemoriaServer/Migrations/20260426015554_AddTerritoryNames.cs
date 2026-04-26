using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TerritoryNames",
                columns: table => new
                {
                    TerritoryId = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryNames", x => x.TerritoryId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerritoryNames");
        }
    }
}
