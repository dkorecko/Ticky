using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class RepeatCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepeatInfo_Number",
                table: "Cards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepeatInfo_Selected",
                table: "Cards",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "RepeatInfo_Time",
                table: "Cards",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatInfo_Type",
                table: "Cards",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepeatInfo_Number",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RepeatInfo_Selected",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RepeatInfo_Time",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RepeatInfo_Type",
                table: "Cards");
        }
    }
}
