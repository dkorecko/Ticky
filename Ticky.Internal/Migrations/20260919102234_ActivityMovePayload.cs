using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class ActivityMovePayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityType",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FromColumnId",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromColumnName",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ToColumnFinished",
                table: "Activities",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToColumnId",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToColumnName",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActivityType_CreatedAt",
                table: "Activities",
                columns: new[] { "ActivityType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_FromColumnId",
                table: "Activities",
                column: "FromColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ToColumnId",
                table: "Activities",
                column: "ToColumnId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Columns_FromColumnId",
                table: "Activities",
                column: "FromColumnId",
                principalTable: "Columns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Columns_ToColumnId",
                table: "Activities",
                column: "ToColumnId",
                principalTable: "Columns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Columns_FromColumnId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Columns_ToColumnId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ActivityType_CreatedAt",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_FromColumnId",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ToColumnId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "FromColumnId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "FromColumnName",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ToColumnFinished",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ToColumnId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ToColumnName",
                table: "Activities");
        }
    }
}
