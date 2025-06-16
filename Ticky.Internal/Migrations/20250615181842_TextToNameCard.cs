using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class TextToNameCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Labels",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Cards",
                newName: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Labels",
                newName: "Text");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Cards",
                newName: "Text");
        }
    }
}
