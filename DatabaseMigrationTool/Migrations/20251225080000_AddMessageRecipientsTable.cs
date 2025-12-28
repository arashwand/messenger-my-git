using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRecipientsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageRecipients",
                columns: table => new
                {
                    MessageRecipientID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageID = table.Column<long>(type: "bigint", nullable: false),
                    RecipientUserID = table.Column<long>(type: "bigint", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadDateTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRecipients", x => x.MessageRecipientID);
                    table.ForeignKey(
                        name: "FK_MessageRecipients_Messages",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "MessageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageRecipients_Users",
                        column: x => x.RecipientUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageRecipients_Message_Recipient",
                table: "MessageRecipients",
                columns: new[] { "MessageID", "RecipientUserID" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageRecipients_RecipientUserId",
                table: "MessageRecipients",
                column: "RecipientUserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageRecipients");
        }
    }
}
