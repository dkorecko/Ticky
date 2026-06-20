using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Ticky.Internal.Data;

#nullable disable

namespace Ticky.Internal.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260620000001_CardColumnHistory")]
    /// <inheritdoc />
    public partial class CardColumnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardColumnHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    FromColumnId = table.Column<int>(type: "int", nullable: true),
                    FromColumnName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToColumnId = table.Column<int>(type: "int", nullable: true),
                    ToColumnName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToColumnFinished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardColumnHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardColumnHistories_AspNetUsers_MovedByUserId",
                        column: x => x.MovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CardColumnHistories_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardColumnHistories_Columns_FromColumnId",
                        column: x => x.FromColumnId,
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CardColumnHistories_Columns_ToColumnId",
                        column: x => x.ToColumnId,
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CardColumnHistories_CardId",
                table: "CardColumnHistories",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardColumnHistories_FromColumnId",
                table: "CardColumnHistories",
                column: "FromColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_CardColumnHistories_MovedByUserId",
                table: "CardColumnHistories",
                column: "MovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardColumnHistories_ToColumnId",
                table: "CardColumnHistories",
                column: "ToColumnId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardColumnHistories");
        }
    }
}
