using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaScope.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldIdsToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ushort>(
                name: "HomeWorldId",
                table: "Players",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ushort>(
                name: "CurrentWorldId",
                table: "Players",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeWorldId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CurrentWorldId",
                table: "Players");
        }
    }
}