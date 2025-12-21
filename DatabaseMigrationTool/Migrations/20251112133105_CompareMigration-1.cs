using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class CompareMigration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.RenameIndex(
            //    name: "IX_ClassGroups_TeacherUserID",
            //    table: "ClassGroups",
            //    newName: "IX_ClassGroups_TeacherUserId");

            //migrationBuilder.AlterDatabase(
            //    collation: "Persian_100_CS_AI");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemMessage",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PersonelChatAccess",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    PersonelId = table.Column<long>(type: "bigint", nullable: false),
                    AccessSendMessageInChat = table.Column<bool>(type: "bit", nullable: false, comment: "مجاز برای ارسال پیام در گروه چت یا کانال\r\n"),
                    AccessToStudentMessage = table.Column<bool>(type: "bit", nullable: false, comment: "مجاز برای ارسال پیام به اعضای گروه چت یا کانال"),
                    GroupType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, comment: "ClassGroup = group\r\nChannelGroup = channel\r\n"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonelChatAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonelChatAccess_Users",
                        column: x => x.PersonelId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                },
                comment: "نگهداری ایدی چت و پرسنل جهت اینکه پرسنل مورد نظر دسترسی به ارسال پیام در گروه و ارسال به افراد ان گروه را دارد یا خیر.\r\n");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelChatAccess_PersonelId",
                table: "PersonelChatAccess",
                column: "PersonelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonelChatAccess");

            migrationBuilder.DropColumn(
                name: "IsSystemMessage",
                table: "Messages");

            //migrationBuilder.RenameIndex(
            //    name: "IX_ClassGroups_TeacherUserId",
            //    table: "ClassGroups",
            //    newName: "IX_ClassGroups_TeacherUserID");

            //migrationBuilder.AlterDatabase(
            //    oldCollation: "Persian_100_CS_AI");
        }
    }
}
