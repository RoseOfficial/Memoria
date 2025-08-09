using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace AlphaScope.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMountsDataToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LodestoneMountsData",
                table: "Players",
                type: "TEXT",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMountsDataUpdate",
                table: "Players",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LodestoneMountsData",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastMountsDataUpdate",
                table: "Players");
        }
    }
}