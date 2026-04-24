using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTotalContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalContributions",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalContributions",
                table: "Users");
        }
    }
}
