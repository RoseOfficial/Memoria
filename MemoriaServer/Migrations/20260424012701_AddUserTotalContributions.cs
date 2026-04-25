using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MemoriaServer.Data;

#nullable disable

namespace MemoriaServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MemoriaDbContext))]
    [Migration("20260424012701_AddUserTotalContributions")]
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
