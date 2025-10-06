using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticky.Internal.Migrations
{
    /// <inheritdoc />
    public partial class SubtaskAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubtaskUser",
                columns: table => new
                {
                    AssigneesId = table.Column<int>(type: "int", nullable: false),
                    SubtasksId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtaskUser", x => new { x.AssigneesId, x.SubtasksId });
                    table.ForeignKey(
                        name: "FK_SubtaskUser_AspNetUsers_AssigneesId",
                        column: x => x.AssigneesId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubtaskUser_Subtasks_SubtasksId",
                        column: x => x.SubtasksId,
                        principalTable: "Subtasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SubtaskUser_SubtasksId",
                table: "SubtaskUser",
                column: "SubtasksId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubtaskUser");
        }
    }
}
