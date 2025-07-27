using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaScopeServer.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDataToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastJobDataUpdate",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LodestoneJobData",
                table: "Players",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MainJobId",
                table: "Players",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MainJobLevel",
                table: "Players",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastJobDataUpdate",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LodestoneJobData",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MainJobId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MainJobLevel",
                table: "Players");
        }
    }
}
