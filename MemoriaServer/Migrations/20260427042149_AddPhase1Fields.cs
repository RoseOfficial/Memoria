using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase1Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentMinionId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentMountId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeCompanyTag",
                table: "Players",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "GrandCompanyId",
                table: "Players",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LodestonePortraitUrl",
                table: "Players",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "OnlineStatusId",
                table: "Players",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitleId",
                table: "Players",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentMinionId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CurrentMountId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "FreeCompanyTag",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "GrandCompanyId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LodestonePortraitUrl",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "OnlineStatusId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TitleId",
                table: "Players");
        }
    }
}
