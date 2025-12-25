using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMessagePrivateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessagePrivate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_MessagePrivate_GetterUserID",
                table: "MessagePrivate",
                column: "GetterUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MessagePrivate_MessageID",
                table: "MessagePrivate",
                column: "MessageID");
        }
    }
}
