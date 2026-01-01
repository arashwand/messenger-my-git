using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class changemessageRecipientcolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageRecipients_Users",
                table: "MessageRecipients");

            migrationBuilder.RenameColumn(
                name: "RecipientUserID",
                table: "MessageRecipients",
                newName: "UserConversationId");

            migrationBuilder.RenameIndex(
                name: "IX_MessageRecipients_RecipientUserId",
                table: "MessageRecipients",
                newName: "IX_MessageRecipients_UserConversationId");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "MessageRecipients",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageRecipients_UserId",
                table: "MessageRecipients",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageRecipients_PrivateChatConversations",
                table: "MessageRecipients",
                column: "UserConversationId",
                principalTable: "PrivateChatConversations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageRecipients_Users_UserId",
                table: "MessageRecipients",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageRecipients_PrivateChatConversations",
                table: "MessageRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageRecipients_Users_UserId",
                table: "MessageRecipients");

            migrationBuilder.DropIndex(
                name: "IX_MessageRecipients_UserId",
                table: "MessageRecipients");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MessageRecipients");

            migrationBuilder.RenameColumn(
                name: "UserConversationId",
                table: "MessageRecipients",
                newName: "RecipientUserID");

            migrationBuilder.RenameIndex(
                name: "IX_MessageRecipients_UserConversationId",
                table: "MessageRecipients",
                newName: "IX_MessageRecipients_RecipientUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageRecipients_Users",
                table: "MessageRecipients",
                column: "RecipientUserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }
    }
}
