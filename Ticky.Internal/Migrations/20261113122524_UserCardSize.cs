using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class UserCardSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CardSize",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardSize",
                table: "AspNetUsers");
        }
    }
}
