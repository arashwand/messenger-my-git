using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileExtension",
                columns: table => new
                {
                    FileExtensionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Extension = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    FontAwesome = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowFileExtention_1", x => x.FileExtensionID);
                });

            migrationBuilder.CreateTable(
                name: "MessengerEventLog",
                columns: table => new
                {
                    MessengerLogID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLog", x => x.MessengerLogID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    RoleName = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    RoleFaName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameFamily = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DeptName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProfilePicName = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: true),
                    LoginToken = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "BlockedUsers",
                columns: table => new
                {
                    BlockedUserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedUsers", x => x.BlockedUserID);
                    table.ForeignKey(
                        name: "FK_BlockedUsers_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    ChannelID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    ChannelName = table.Column<string>(type: "varchar(250)", unicode: false, maxLength: 250, nullable: false),
                    ChannelTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.ChannelID);
                    table.ForeignKey(
                        name: "FK_Channels_Users",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ClassGroups",
                columns: table => new
                {
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    LevelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TeacherUserID = table.Column<long>(type: "bigint", nullable: false),
                    ClassTiming = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    isActive = table.Column<bool>(type: "bit", nullable: false),
                    LeftSes = table.Column<int>(type: "int", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    ClassGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroups", x => x.ClassID);
                    table.ForeignKey(
                        name: "FK_ClassGroups_Users",
                        column: x => x.TeacherUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReplyMessageID = table.Column<long>(type: "bigint", nullable: true),
                    MessageDateTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    LastEditDateTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    SenderUserID = table.Column<long>(type: "bigint", nullable: false),
                    isPin = table.Column<bool>(type: "bit", nullable: false),
                    isHidden = table.Column<bool>(type: "bit", nullable: false),
                    isEdited = table.Column<bool>(type: "bit", nullable: false),
                    MessageType = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageID);
                    table.ForeignKey(
                        name: "FK_Messages_Messages",
                        column: x => x.ReplyMessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID");
                    table.ForeignKey(
                        name: "FK_Messages_Users",
                        column: x => x.SenderUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ChannelMembers",
                columns: table => new
                {
                    ChanelMemberID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    ChannelID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    MemberRoleType = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMembers", x => x.ChanelMemberID);
                    table.ForeignKey(
                        name: "FK_ChannelMembers_Channels",
                        column: x => x.ChannelID,
                        principalTable: "Channels",
                        principalColumn: "ChannelID");
                    table.ForeignKey(
                        name: "FK_ChannelMembers_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "UserClassGroup",
                columns: table => new
                {
                    UserClassGroupID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    LastReadMessageId = table.Column<long>(type: "bigint", nullable: true),
                    MemberRoleType = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClassGroup_1", x => x.UserClassGroupID);
                    table.ForeignKey(
                        name: "FK_UserClassGroup_ClassGroups",
                        column: x => x.ClassID,
                        principalTable: "ClassGroups",
                        principalColumn: "ClassID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserClassGroup_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ViewClassGroup",
                columns: table => new
                {
                    ViewClassGroupID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ViewDateTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    ClassID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewClassGroup", x => x.ViewClassGroupID);
                    table.ForeignKey(
                        name: "FK_ViewClassGroup_ClassGroups",
                        column: x => x.ClassID,
                        principalTable: "ClassGroups",
                        principalColumn: "ClassID");
                    table.ForeignKey(
                        name: "FK_ViewClassGroup_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ChannelMessages",
                columns: table => new
                {
                    ChannelMessageID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChannelID = table.Column<int>(type: "int", nullable: false),
                    MessageID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.ChannelMessageID);
                    table.ForeignKey(
                        name: "FK_ChannelMessages_Channels",
                        column: x => x.ChannelID,
                        principalTable: "Channels",
                        principalColumn: "ChannelID");
                    table.ForeignKey(
                        name: "FK_ChannelMessages_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassGroupMessages",
                columns: table => new
                {
                    ClassGroupMessageID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    MessageID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroupMessages", x => x.ClassGroupMessageID);
                    table.ForeignKey(
                        name: "FK_ClassGroupMessages_ClassGroups",
                        column: x => x.ClassID,
                        principalTable: "ClassGroups",
                        principalColumn: "ClassID");
                    table.ForeignKey(
                        name: "FK_ClassGroupMessages_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageFile",
                columns: table => new
                {
                    MessageFileID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileExtensionID = table.Column<int>(type: "int", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploaderUserId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileThumbPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "if file type is Image, this filed has value")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageFile", x => x.MessageFileID);
                    table.ForeignKey(
                        name: "FK_MessageFile_FileExtension",
                        column: x => x.FileExtensionID,
                        principalTable: "FileExtension",
                        principalColumn: "FileExtensionID");
                    table.ForeignKey(
                        name: "FK_MessageFile_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageFoulReport",
                columns: table => new
                {
                    MessageFoulReportID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FoulReportDateTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    FoulReporterUserID = table.Column<long>(type: "bigint", nullable: false),
                    FoulDesc = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageFoulReport", x => x.MessageFoulReportID);
                    table.ForeignKey(
                        name: "FK_MessageFoulReport_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageFoulReport_Users",
                        column: x => x.FoulReporterUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "MessagePrivate",
                columns: table => new
                {
                    MessagePrivateID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    GetterUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagePrivate", x => x.MessagePrivateID);
                    table.ForeignKey(
                        name: "FK_MessagePrivate_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessagePrivate_Users",
                        column: x => x.GetterUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "MessageRead",
                columns: table => new
                {
                    ReadMessageID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    ReadDateTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    GroupType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, comment: "ClassGroup = group\r\nChannelGroup = channel\r\nPrivate = private chat"),
                    TargetId = table.Column<int>(type: "int", nullable: false, comment: "ایدی چت مورد نظر - گروه یا کانال ")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRead", x => x.ReadMessageID);
                    table.ForeignKey(
                        name: "FK_MessageRead_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageRead_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                },
                comment: "");

            migrationBuilder.CreateTable(
                name: "MessageSaved",
                columns: table => new
                {
                    MessageSavedID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    SaveDateTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageSaved", x => x.MessageSavedID);
                    table.ForeignKey(
                        name: "FK_MessageSaved_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageSaved_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "MessageText",
                columns: table => new
                {
                    MessageTextID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    MessageTxt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageText", x => x.MessageTextID);
                    table.ForeignKey(
                        name: "FK_MessageText_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedUsers_UserID",
                table: "BlockedUsers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMembers_ChannelID",
                table: "ChannelMembers",
                column: "ChannelID");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMembers_UserID",
                table: "ChannelMembers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_ChannelID",
                table: "ChannelMessages",
                column: "ChannelID");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_MessageID",
                table: "ChannelMessages",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_CreatorUserId",
                table: "Channels",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroupMessages_ClassID",
                table: "ClassGroupMessages",
                column: "ClassID");

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroupMessages_MessageID",
                table: "ClassGroupMessages",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroups_TeacherUserID",
                table: "ClassGroups",
                column: "TeacherUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFile_FileExtensionID",
                table: "MessageFile",
                column: "FileExtensionID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFile_MessageID",
                table: "MessageFile",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFoulReport_FoulReporterUserID",
                table: "MessageFoulReport",
                column: "FoulReporterUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFoulReport_MessageID",
                table: "MessageFoulReport",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePrivate_GetterUserID",
                table: "MessagePrivate",
                column: "GetterUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePrivate_MessageID",
                table: "MessagePrivate",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRead_MessageID",
                table: "MessageRead",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRead_User_Group_Target_Message",
                table: "MessageRead",
                columns: new[] { "UserID", "GroupType", "TargetId", "MessageID" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyMessageID",
                table: "Messages",
                column: "ReplyMessageID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderUserID",
                table: "Messages",
                column: "SenderUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageSaved_MessageID",
                table: "MessageSaved",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageSaved_UserID",
                table: "MessageSaved",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessageText_MessageID",
                table: "MessageText",
                column: "MessageID");

            migrationBuilder.CreateIndex(
                name: "IX_UserClassGroup_ClassID",
                table: "UserClassGroup",
                column: "ClassID");

            migrationBuilder.CreateIndex(
                name: "IX_UserClassGroup_UserID",
                table: "UserClassGroup",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ViewClassGroup_ClassID",
                table: "ViewClassGroup",
                column: "ClassID");

            migrationBuilder.CreateIndex(
                name: "IX_ViewClassGroup_UserID",
                table: "ViewClassGroup",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedUsers");

            migrationBuilder.DropTable(
                name: "ChannelMembers");

            migrationBuilder.DropTable(
                name: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "ClassGroupMessages");

            migrationBuilder.DropTable(
                name: "MessageFile");

            migrationBuilder.DropTable(
                name: "MessageFoulReport");

            migrationBuilder.DropTable(
                name: "MessagePrivate");

            migrationBuilder.DropTable(
                name: "MessageRead");

            migrationBuilder.DropTable(
                name: "MessageSaved");

            migrationBuilder.DropTable(
                name: "MessageText");

            migrationBuilder.DropTable(
                name: "MessengerEventLog");

            migrationBuilder.DropTable(
                name: "UserClassGroup");

            migrationBuilder.DropTable(
                name: "ViewClassGroup");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "FileExtension");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ClassGroups");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
