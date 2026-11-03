using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class TypedActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Activities",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ActivityType",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "At",
                table: "Activities",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "Activities",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Duration",
                table: "Activities",
                type: "bigint",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "OldDuration",
                table: "Activities",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldName",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatType",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtaskText",
                table: "Activities",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TargetId",
                table: "Activities",
                type: "int",
                nullable: true);

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

            // Existing rows are converted from their text at startup by LegacyActivityConverter.
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
                name: "At",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "FromColumnId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "FromColumnName",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OldDuration",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "OldName",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RepeatType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SubtaskText",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TargetId",
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

            // Rows converted to data have no text anymore; rolling back leaves them empty.
            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Text",
                keyValue: null,
                column: "Text",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Activities",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
