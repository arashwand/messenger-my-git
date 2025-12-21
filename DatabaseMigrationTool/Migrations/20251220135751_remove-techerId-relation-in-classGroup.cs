using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class removetecherIdrelationinclassGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassGroups_Users",
                table: "ClassGroups");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "ClassGroups",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroups_UserId",
                table: "ClassGroups",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassGroups_Users_UserId",
                table: "ClassGroups",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassGroups_Users_UserId",
                table: "ClassGroups");

            migrationBuilder.DropIndex(
                name: "IX_ClassGroups_UserId",
                table: "ClassGroups");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ClassGroups");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassGroups_Users",
                table: "ClassGroups",
                column: "TeacherUserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }
    }
}
