using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class SelfAssign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticAssign",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomaticAssign",
                table: "AspNetUsers");
        }
    }
}
