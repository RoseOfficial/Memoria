using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace AlphaScope.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMinionsDataToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LodestoneMinionsData",
                table: "Players",
                type: "TEXT",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMinionsDataUpdate",
                table: "Players",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LodestoneMinionsData",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastMinionsDataUpdate",
                table: "Players");
        }
    }
}