using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaScopeServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMinionsDataToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastMinionsDataUpdate",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LodestoneMinionsData",
                table: "Players",
                type: "TEXT",
                maxLength: 10000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMinionsDataUpdate",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LodestoneMinionsData",
                table: "Players");
        }
    }
}
