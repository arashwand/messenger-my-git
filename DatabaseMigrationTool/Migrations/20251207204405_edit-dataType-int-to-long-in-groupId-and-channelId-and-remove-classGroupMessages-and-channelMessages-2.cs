using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseMigrationTool.Migrations
{
    /// <inheritdoc />
    public partial class editdataTypeinttolongingroupIdandchannelIdandremoveclassGroupMessagesandchannelMessages2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Drop all foreign keys that reference ClassGroups.ClassID or Channels.ChannelID (dynamic, safe)
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) +
               ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(13)
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
WHERE (rc.object_id = OBJECT_ID('dbo.ClassGroups') AND rc.name = 'ClassID')
   OR (rc.object_id = OBJECT_ID('dbo.Channels') AND rc.name = 'ChannelID');

IF @sql <> '' EXEC sp_executesql @sql;
");

            // 2) Drop non-PK indexes on all columns we're about to alter (dynamic, safe)
            migrationBuilder.Sql(@"
DECLARE @dropIdx nvarchar(max) = N'';
SELECT @dropIdx = @dropIdx 
    + 'DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON t.object_id = i.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0
  AND (
    (t.name = 'ClassGroups' AND c.name = 'ClassID')
    OR (t.name = 'Channels' AND c.name = 'ChannelID')
    OR (t.name = 'UserClassGroup' AND c.name = 'ClassID')
    OR (t.name = 'ViewClassGroup' AND c.name = 'ClassID')
    OR (t.name = 'ChannelMembers' AND c.name = 'ChannelID')
    OR (t.name = 'Messages' AND c.name = 'OwnerId')
    OR (t.name = 'MessageRead' AND c.name = 'TargetId')
    OR (t.name = 'PersonelChatAccess' AND c.name = 'TargetId')
  );

IF @dropIdx <> '' EXEC sp_executesql @dropIdx;
");

            // 3) Drop join tables safely if present
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ChannelMessages', N'U') IS NOT NULL DROP TABLE dbo.ChannelMessages;
IF OBJECT_ID(N'dbo.ClassGroupMessages', N'U') IS NOT NULL DROP TABLE dbo.ClassGroupMessages;
");

            // 4) Drop PKs on parent tables if they exist
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.ClassGroups'))
    ALTER TABLE dbo.ClassGroups DROP CONSTRAINT PK_ClassGroups;

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_Channels' AND parent_object_id = OBJECT_ID('dbo.Channels'))
    ALTER TABLE dbo.Channels DROP CONSTRAINT PK_Channels;
");

            // 4.5) Drop default constraints on columns we're about to alter
            migrationBuilder.Sql(@"
DECLARE @dropDefaults nvarchar(max) = N'';

SELECT @dropDefaults = @dropDefaults 
    + 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) 
    + ' DROP CONSTRAINT ' + QUOTENAME(dc.name) + ';' + CHAR(13)
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
INNER JOIN sys.tables t ON t.object_id = dc.parent_object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE 
    (t.name = 'Messages' AND c.name = 'OwnerId')
    OR (t.name = 'MessageRead' AND c.name = 'TargetId')
    OR (t.name = 'PersonelChatAccess' AND c.name = 'TargetId')
    OR (t.name = 'ClassGroups' AND c.name = 'ClassID')
    OR (t.name = 'Channels' AND c.name = 'ChannelID')
    OR (t.name = 'UserClassGroup' AND c.name = 'ClassID')
    OR (t.name = 'ViewClassGroup' AND c.name = 'ClassID')
    OR (t.name = 'ChannelMembers' AND c.name = 'ChannelID');

IF @dropDefaults <> '' EXEC sp_executesql @dropDefaults;
");

            // 5) Alter columns using raw SQL to bypass EF index detection issues
            migrationBuilder.Sql("ALTER TABLE dbo.ClassGroups ALTER COLUMN ClassID bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.UserClassGroup ALTER COLUMN ClassID bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.ViewClassGroup ALTER COLUMN ClassID bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.Channels ALTER COLUMN ChannelID bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.ChannelMembers ALTER COLUMN ChannelID bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.PersonelChatAccess ALTER COLUMN TargetId bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.Messages ALTER COLUMN OwnerId bigint NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.MessageRead ALTER COLUMN TargetId bigint NOT NULL;");

            // 6) Recreate PKs on parent tables
            migrationBuilder.Sql(@"
ALTER TABLE dbo.ClassGroups ADD CONSTRAINT PK_ClassGroups PRIMARY KEY (ClassID);
ALTER TABLE dbo.Channels ADD CONSTRAINT PK_Channels PRIMARY KEY (ChannelID);
");

            // 7) Recreate necessary indexes and foreign keys conditionally

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Channels_CreatorUserId' AND object_id = OBJECT_ID('dbo.Channels'))
    CREATE INDEX IX_Channels_CreatorUserId ON dbo.Channels (CreatorUserId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Channels_Users' AND parent_object_id = OBJECT_ID('dbo.Channels'))
    ALTER TABLE dbo.Channels ADD CONSTRAINT FK_Channels_Users FOREIGN KEY (CreatorUserId) REFERENCES dbo.Users(UserID);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChannelMembers_ChannelID' AND object_id = OBJECT_ID('dbo.ChannelMembers'))
    CREATE INDEX IX_ChannelMembers_ChannelID ON dbo.ChannelMembers (ChannelID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChannelMembers_Channels' AND parent_object_id = OBJECT_ID('dbo.ChannelMembers'))
    ALTER TABLE dbo.ChannelMembers ADD CONSTRAINT FK_ChannelMembers_Channels FOREIGN KEY (ChannelID) REFERENCES dbo.Channels(ChannelID);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserClassGroup_ClassID' AND object_id = OBJECT_ID('dbo.UserClassGroup'))
    CREATE INDEX IX_UserClassGroup_ClassID ON dbo.UserClassGroup (ClassID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserClassGroup_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.UserClassGroup'))
    ALTER TABLE dbo.UserClassGroup ADD CONSTRAINT FK_UserClassGroup_ClassGroups FOREIGN KEY (ClassID) REFERENCES dbo.ClassGroups(ClassID) ON DELETE CASCADE;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ViewClassGroup_ClassID' AND object_id = OBJECT_ID('dbo.ViewClassGroup'))
    CREATE INDEX IX_ViewClassGroup_ClassID ON dbo.ViewClassGroup (ClassID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ViewClassGroup_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.ViewClassGroup'))
    ALTER TABLE dbo.ViewClassGroup ADD CONSTRAINT FK_ViewClassGroup_ClassGroups FOREIGN KEY (ClassID) REFERENCES dbo.ClassGroups(ClassID);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FKs that reference ClassGroups.ClassID or Channels.ChannelID
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) +
               ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' + CHAR(13)
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
WHERE (rc.object_id = OBJECT_ID('dbo.ClassGroups') AND rc.name = 'ClassID')
   OR (rc.object_id = OBJECT_ID('dbo.Channels') AND rc.name = 'ChannelID');

IF @sql <> '' EXEC sp_executesql @sql;
");

            // Drop non-PK indexes on ClassID/ChannelID
            migrationBuilder.Sql(@"
DECLARE @dropIdx nvarchar(max) = N'';
SELECT @dropIdx = @dropIdx 
    + 'DROP INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON t.object_id = i.object_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE c.name IN ('ClassID','ChannelID') AND i.is_primary_key = 0 AND i.is_unique_constraint = 0;

IF @dropIdx <> '' EXEC sp_executesql @dropIdx;
");

            // Drop PKs
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_Channels' AND parent_object_id = OBJECT_ID('dbo.Channels'))
    ALTER TABLE dbo.Channels DROP CONSTRAINT PK_Channels;

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.ClassGroups'))
    ALTER TABLE dbo.ClassGroups DROP CONSTRAINT PK_ClassGroups;
");

            // Revert column types using raw SQL
            migrationBuilder.Sql("ALTER TABLE dbo.ClassGroups ALTER COLUMN ClassID int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.UserClassGroup ALTER COLUMN ClassID int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.ViewClassGroup ALTER COLUMN ClassID int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.Channels ALTER COLUMN ChannelID int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.ChannelMembers ALTER COLUMN ChannelID int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.PersonelChatAccess ALTER COLUMN TargetId int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.Messages ALTER COLUMN OwnerId int NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE dbo.MessageRead ALTER COLUMN TargetId int NOT NULL;");

            // Recreate PKs
            migrationBuilder.Sql(@"
ALTER TABLE dbo.ClassGroups ADD CONSTRAINT PK_ClassGroups PRIMARY KEY (ClassID);
ALTER TABLE dbo.Channels ADD CONSTRAINT PK_Channels PRIMARY KEY (ChannelID);
");

            // Recreate indexes and FKs
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChannelMembers_ChannelID' AND object_id = OBJECT_ID('dbo.ChannelMembers'))
    CREATE INDEX IX_ChannelMembers_ChannelID ON dbo.ChannelMembers (ChannelID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChannelMembers_Channels' AND parent_object_id = OBJECT_ID('dbo.ChannelMembers'))
    ALTER TABLE dbo.ChannelMembers ADD CONSTRAINT FK_ChannelMembers_Channels FOREIGN KEY (ChannelID) REFERENCES dbo.Channels(ChannelID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserClassGroup_ClassID' AND object_id = OBJECT_ID('dbo.UserClassGroup'))
    CREATE INDEX IX_UserClassGroup_ClassID ON dbo.UserClassGroup (ClassID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserClassGroup_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.UserClassGroup'))
    ALTER TABLE dbo.UserClassGroup ADD CONSTRAINT FK_UserClassGroup_ClassGroups FOREIGN KEY (ClassID) REFERENCES dbo.ClassGroups(ClassID) ON DELETE CASCADE;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ViewClassGroup_ClassID' AND object_id = OBJECT_ID('dbo.ViewClassGroup'))
    CREATE INDEX IX_ViewClassGroup_ClassID ON dbo.ViewClassGroup (ClassID);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ViewClassGroup_ClassGroups' AND parent_object_id = OBJECT_ID('dbo.ViewClassGroup'))
    ALTER TABLE dbo.ViewClassGroup ADD CONSTRAINT FK_ViewClassGroup_ClassGroups FOREIGN KEY (ClassID) REFERENCES dbo.ClassGroups(ClassID);
");
        }
    }
}