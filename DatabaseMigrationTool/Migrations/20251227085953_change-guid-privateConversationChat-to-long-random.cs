using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class changeguidprivateConversationChattolongrandom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. ابتدا ایندکس‌های مرتبط با این ستون را حذف می‌کنیم
            migrationBuilder.DropIndex(
                name: "IX_PrivateChatConversations_ConversationId",
                table: "PrivateChatConversations");

            // 2. حذف کلیدهای خارجی که به این ستون اشاره می‌کنند
            migrationBuilder.Sql(@"
            -- پیدا کردن و حذف کلیدهای خارجی که به این ستون اشاره می‌کنند
            DECLARE @ForeignKeyName NVARCHAR(128);
            DECLARE @TableName NVARCHAR(128);
            
            DECLARE foreignKeyCursor CURSOR FOR
            SELECT 
                fk.name AS ForeignKeyName,
                OBJECT_NAME(fk.parent_object_id) AS TableName
            FROM 
                sys.foreign_keys fk
            INNER JOIN 
                sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN 
                sys.columns c ON fkc.parent_column_id = c.column_id 
                AND fkc.parent_object_id = c.object_id
            INNER JOIN
                sys.tables t ON fk.referenced_object_id = t.object_id
            WHERE 
                t.name = 'PrivateChatConversations'
                AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) = 'ConversationId';
            
            OPEN foreignKeyCursor;
            FETCH NEXT FROM foreignKeyCursor INTO @ForeignKeyName, @TableName;
            
            WHILE @@FETCH_STATUS = 0
            BEGIN
                EXEC('ALTER TABLE [' + @TableName + '] DROP CONSTRAINT [' + @ForeignKeyName + ']');
                FETCH NEXT FROM foreignKeyCursor INTO @ForeignKeyName, @TableName;
            END
            
            CLOSE foreignKeyCursor;
            DEALLOCATE foreignKeyCursor;
        ");

            // 3. اگر محدودیت DEFAULT وجود دارد، حذف می‌کنیم
            migrationBuilder.Sql(@"
            DECLARE @ConstraintName sysname;
            SELECT @ConstraintName = [d].[name]
            FROM [sys].[default_constraints] [d]
            INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] 
                AND [d].[parent_object_id] = [c].[object_id]
            WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PrivateChatConversations]') 
                AND [c].[name] = N'ConversationId');
            
            IF @ConstraintName IS NOT NULL 
                EXEC(N'ALTER TABLE [PrivateChatConversations] DROP CONSTRAINT [' + @ConstraintName + '];');
        ");

            // 4. ابتدا یک ستون موقت اضافه می‌کنیم
            migrationBuilder.AddColumn<long>(
                name: "NewConversationId",
                table: "PrivateChatConversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // 5. مقادیر تصادفی برای ستون جدید تولید می‌کنیم
            migrationBuilder.Sql(@"
            UPDATE [PrivateChatConversations] 
            SET [NewConversationId] = ABS(CHECKSUM(NEWID())) % 9999999999;
        ");

            // 6. ستون قدیمی را حذف می‌کنیم
            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "PrivateChatConversations");

            // 7. ستون جدید را به نام قدیمی تغییر نام می‌دهیم
            migrationBuilder.RenameColumn(
                name: "NewConversationId",
                table: "PrivateChatConversations",
                newName: "ConversationId");

            // 8. ایندکس جدید ایجاد می‌کنیم
            migrationBuilder.CreateIndex(
                name: "IX_PrivateChatConversations_ConversationId",
                table: "PrivateChatConversations",
                column: "ConversationId",
                unique: true);

            // 9. کلید اولیه را روی ستون جدید تنظیم می‌کنیم (اگر نیاز است)
            // اگر ConversationId کلید اولیه است:
            // migrationBuilder.AddPrimaryKey(
            //     name: "PK_PrivateChatConversations",
            //     table: "PrivateChatConversations",
            //     column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. ابتدا ایندکس را حذف می‌کنیم
            migrationBuilder.DropIndex(
                name: "IX_PrivateChatConversations_ConversationId",
                table: "PrivateChatConversations");

            // 2. ستون موقت برای GUID اضافه می‭‌کنیم
            migrationBuilder.AddColumn<Guid>(
                name: "TempConversationId",
                table: "PrivateChatConversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // 3. مقادیر GUID تصادفی تولید می‌کنیم
            migrationBuilder.Sql(@"
            UPDATE [PrivateChatConversations] 
            SET [TempConversationId] = NEWID();
        ");

            // 4. ستون long را حذف می‌کنیم
            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "PrivateChatConversations");

            // 5. ستون GUID را به نام اصلی تغییر نام می‌دهیم
            migrationBuilder.RenameColumn(
                name: "TempConversationId",
                table: "PrivateChatConversations",
                newName: "ConversationId");

            // 6. ایندکس را دوباره ایجاد می‌کنیم
            migrationBuilder.CreateIndex(
                name: "IX_PrivateChatConversations_ConversationId",
                table: "PrivateChatConversations",
                column: "ConversationId",
                unique: true);
        }
    }
}
