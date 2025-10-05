using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class BlockToFlagged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Blocked",
                table: "Cards",
                newName: "Flagged");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Flagged",
                table: "Cards",
                newName: "Blocked");
        }
    }
}
