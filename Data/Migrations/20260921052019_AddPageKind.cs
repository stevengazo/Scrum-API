using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrum.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPageKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Pages",
                type: "TEXT",
                nullable: false,
                defaultValue: "Page");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Pages");
        }
    }
}
