using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DatabaseMigrationTool.Models;

public partial class MyDbContext : DbContext
{
    public MyDbContext()
    {
    }

    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BlockedUser> BlockedUsers { get; set; }

    public virtual DbSet<Channel> Channels { get; set; }

    public virtual DbSet<ChannelMember> ChannelMembers { get; set; }

    public virtual DbSet<ChannelMessage> ChannelMessages { get; set; }

    public virtual DbSet<ClassGroup> ClassGroups { get; set; }

    public virtual DbSet<ClassGroupMessage> ClassGroupMessages { get; set; }

    public virtual DbSet<ClassGroupMessagesView> ClassGroupMessagesViews { get; set; }

    public virtual DbSet<FileExtension> FileExtensions { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageFile> MessageFiles { get; set; }

    public virtual DbSet<MessageFoulReport> MessageFoulReports { get; set; }

    public virtual DbSet<MessagePrivate> MessagePrivates { get; set; }

    public virtual DbSet<MessageRead> MessageReads { get; set; }

    public virtual DbSet<MessageSaved> MessageSaveds { get; set; }

    public virtual DbSet<MessageText> MessageTexts { get; set; }

    public virtual DbSet<MessengerEventLog> MessengerEventLogs { get; set; }

    public virtual DbSet<PersonelChatAccess> PersonelChatAccesses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserClassGroup> UserClassGroups { get; set; }

    public virtual DbSet<ViewClassGroup> ViewClassGroups { get; set; }

    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //     => optionsBuilder.UseSqlServer("Server=94.232.173.69;Database=MessengerDB;User Id=sa;Password=RLK07MJczpKGFkV2srPk;TrustServerCertificate=True;");
// Assuming this is added in the builder.Services section, after var builder = WebApplication.CreateBuilder(args);


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.UseCollation("Persian_100_CS_AI");

        modelBuilder.Entity<BlockedUser>(entity =>
        {
            entity.Property(e => e.BlockedUserId).HasColumnName("BlockedUserID");
            entity.Property(e => e.BlockDate).HasColumnType("datetime");
            entity.Property(e => e.Comment).HasMaxLength(800);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.BlockedUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BlockedUsers_Users");
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.ChannelName)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.ChannelTitle).HasMaxLength(500);
            entity.Property(e => e.CreateDate).HasColumnType("datetime");

            entity.HasOne(d => d.CreatorUser).WithMany(p => p.Channels)
                .HasForeignKey(d => d.CreatorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Channels_Users");
        });

        modelBuilder.Entity<ChannelMember>(entity =>
        {
            entity.HasKey(e => e.ChanelMemberId);

            entity.Property(e => e.ChanelMemberId).HasColumnName("ChanelMemberID");
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Channel).WithMany(p => p.ChannelMembers)
                .HasForeignKey(d => d.ChannelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChannelMembers_Channels");

            entity.HasOne(d => d.User).WithMany(p => p.ChannelMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChannelMembers_Users");
        });

        modelBuilder.Entity<ChannelMessage>(entity =>
        {
            entity.Property(e => e.ChannelMessageId).HasColumnName("ChannelMessageID");
            entity.Property(e => e.ChannelId).HasColumnName("ChannelID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");

            entity.HasOne(d => d.Channel).WithMany(p => p.ChannelMessages)
                .HasForeignKey(d => d.ChannelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChannelMessages_Channels");

            entity.HasOne(d => d.Message).WithMany(p => p.ChannelMessages)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_ChannelMessages_Messages");
        });

        modelBuilder.Entity<ClassGroup>(entity =>
        {
            entity.HasKey(e => e.ClassId);

            entity.HasIndex(e => e.TeacherUserId, "IX_ClassGroups_TeacherUserId");

            entity.Property(e => e.ClassId)
                .ValueGeneratedNever()
                .HasColumnName("ClassID");
            entity.Property(e => e.ClassGroupName).HasMaxLength(200);
            entity.Property(e => e.ClassTiming).HasMaxLength(500);
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LevelName).HasMaxLength(100);
            entity.Property(e => e.TeacherUserId).HasColumnName("TeacherUserID");

            entity.HasOne(d => d.TeacherUser).WithMany(p => p.ClassGroups)
                .HasForeignKey(d => d.TeacherUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassGroups_Users");
        });

        modelBuilder.Entity<ClassGroupMessage>(entity =>
        {
            entity.Property(e => e.ClassGroupMessageId).HasColumnName("ClassGroupMessageID");
            entity.Property(e => e.ClassId).HasColumnName("ClassID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassGroupMessages)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassGroupMessages_ClassGroups");

            entity.HasOne(d => d.Message).WithMany(p => p.ClassGroupMessages)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_ClassGroupMessages_Messages");
        });

        modelBuilder.Entity<ClassGroupMessagesView>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ClassGroupMessagesView");

            entity.Property(e => e.ClassId).HasColumnName("ClassID");
            entity.Property(e => e.ClassTiming).HasMaxLength(500);
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.IsHidden).HasColumnName("isHidden");
            entity.Property(e => e.IsPin).HasColumnName("isPin");
            entity.Property(e => e.IsPrivate).HasColumnName("isPrivate");
            entity.Property(e => e.LevelName).HasMaxLength(100);
            entity.Property(e => e.MessageDateTime).HasColumnType("datetime");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ReplyMessageId).HasColumnName("ReplyMessageID");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserID");
            entity.Property(e => e.TeacherUserId).HasColumnName("TeacherUserID");
        });

        modelBuilder.Entity<FileExtension>(entity =>
        {
            entity.HasKey(e => e.FileExtensionId).HasName("PK_AllowFileExtention_1");

            entity.ToTable("FileExtension");

            entity.Property(e => e.FileExtensionId).HasColumnName("FileExtensionID");
            entity.Property(e => e.Comment).HasMaxLength(200);
            entity.Property(e => e.Extension)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.FontAwesome)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(200)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.IsEdited).HasColumnName("isEdited");
            entity.Property(e => e.IsHidden).HasColumnName("isHidden");
            entity.Property(e => e.IsPin).HasColumnName("isPin");
            entity.Property(e => e.LastEditDateTime).HasColumnType("datetime");
            entity.Property(e => e.MessageDateTime).HasColumnType("datetime");
            entity.Property(e => e.ReplyMessageId).HasColumnName("ReplyMessageID");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserID");

            entity.HasOne(d => d.ReplyMessage).WithMany(p => p.InverseReplyMessage)
                .HasForeignKey(d => d.ReplyMessageId)
                .HasConstraintName("FK_Messages_Messages");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Messages_Users");
        });

        modelBuilder.Entity<MessageFile>(entity =>
        {
            entity.ToTable("MessageFile");

            entity.Property(e => e.MessageFileId).HasColumnName("MessageFileID");
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
            entity.Property(e => e.FileExtensionId).HasColumnName("FileExtensionID");
            entity.Property(e => e.FileName).HasMaxLength(300);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.FileThumbPath)
                .HasMaxLength(500)
                .HasComment("if file type is Image, this filed has value");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.OriginalFileName).HasMaxLength(500);

            entity.HasOne(d => d.FileExtension).WithMany(p => p.MessageFiles)
                .HasForeignKey(d => d.FileExtensionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageFile_FileExtension");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageFiles)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MessageFile_Messages");
        });

        modelBuilder.Entity<MessageFoulReport>(entity =>
        {
            entity.ToTable("MessageFoulReport");

            entity.Property(e => e.MessageFoulReportId).HasColumnName("MessageFoulReportID");
            entity.Property(e => e.FoulDesc).HasMaxLength(700);
            entity.Property(e => e.FoulReportDateTime).HasColumnType("datetime");
            entity.Property(e => e.FoulReporterUserId).HasColumnName("FoulReporterUserID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");

            entity.HasOne(d => d.FoulReporterUser).WithMany(p => p.MessageFoulReports)
                .HasForeignKey(d => d.FoulReporterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageFoulReport_Users");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageFoulReports)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_MessageFoulReport_Messages");
        });

        modelBuilder.Entity<MessagePrivate>(entity =>
        {
            entity.ToTable("MessagePrivate");

            entity.Property(e => e.MessagePrivateId).HasColumnName("MessagePrivateID");
            entity.Property(e => e.GetterUserId).HasColumnName("GetterUserID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");

            entity.HasOne(d => d.GetterUser).WithMany(p => p.MessagePrivates)
                .HasForeignKey(d => d.GetterUserId)
                .HasConstraintName("FK_MessagePrivate_Users");

            entity.HasOne(d => d.Message).WithMany(p => p.MessagePrivates)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_MessagePrivate_Messages");
        });

        modelBuilder.Entity<MessageRead>(entity =>
        {
            entity.HasKey(e => e.ReadMessageId);

            entity.ToTable("MessageRead", tb => tb.HasComment(""));

            entity.HasIndex(e => new { e.UserId, e.GroupType, e.TargetId, e.MessageId }, "IX_MessageRead_User_Group_Target_Message").IsDescending(false, false, false, true);

            entity.Property(e => e.ReadMessageId).HasColumnName("ReadMessageID");
            entity.Property(e => e.GroupType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasComment("ClassGroup = group\r\nChannelGroup = channel\r\nPrivate = private chat");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ReadDateTime).HasColumnType("datetime");
            entity.Property(e => e.TargetId).HasComment("ایدی چت مورد نظر - گروه یا کانال ");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_MessageRead_Messages");

            entity.HasOne(d => d.User).WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageRead_Users");
        });

        modelBuilder.Entity<MessageSaved>(entity =>
        {
            entity.ToTable("MessageSaved");

            entity.Property(e => e.MessageSavedId).HasColumnName("MessageSavedID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.SaveDateTime).HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageSaveds)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_MessageSaved_Messages");

            entity.HasOne(d => d.User).WithMany(p => p.MessageSaveds)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageSaved_Users");
        });

        modelBuilder.Entity<MessageText>(entity =>
        {
            entity.ToTable("MessageText");

            entity.Property(e => e.MessageTextId).HasColumnName("MessageTextID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageTexts)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_MessageText_Messages");
        });

        modelBuilder.Entity<MessengerEventLog>(entity =>
        {
            entity.HasKey(e => e.MessengerLogId).HasName("PK_EventLog");

            entity.ToTable("MessengerEventLog");

            entity.Property(e => e.MessengerLogId).HasColumnName("MessengerLogID");
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<PersonelChatAccess>(entity =>
        {
            entity.ToTable("PersonelChatAccess", tb => tb.HasComment("نگهداری ایدی چت و پرسنل جهت اینکه پرسنل مورد نظر دسترسی به ارسال پیام در گروه و ارسال به افراد ان گروه را دارد یا خیر.\r\n"));

            entity.Property(e => e.AccessSendMessageInChat).HasComment("مجاز برای ارسال پیام در گروه چت یا کانال\r\n");
            entity.Property(e => e.AccessToStudentMessage).HasComment("مجاز برای ارسال پیام به اعضای گروه چت یا کانال");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.GroupType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasComment("ClassGroup = group\r\nChannelGroup = channel\r\n");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Personel).WithMany(p => p.PersonelChatAccesses)
                .HasForeignKey(d => d.PersonelId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PersonelChatAccess_Users");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("UserID");
            entity.Property(e => e.DeptName).HasMaxLength(50);
            entity.Property(e => e.LoginToken)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.NameFamily).HasMaxLength(250);
            entity.Property(e => e.ProfilePicName)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.RoleFaName).HasMaxLength(50);
            entity.Property(e => e.RoleName)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserClassGroup>(entity =>
        {
            entity.HasKey(e => e.UserClassGroupId).HasName("PK_UserClassGroup_1");

            entity.ToTable("UserClassGroup");

            entity.Property(e => e.UserClassGroupId).HasColumnName("UserClassGroupID");
            entity.Property(e => e.ClassId).HasColumnName("ClassID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Class).WithMany(p => p.UserClassGroups)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("FK_UserClassGroup_ClassGroups");

            entity.HasOne(d => d.User).WithMany(p => p.UserClassGroups)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserClassGroup_Users");
        });

        modelBuilder.Entity<ViewClassGroup>(entity =>
        {
            entity.ToTable("ViewClassGroup");

            entity.Property(e => e.ViewClassGroupId).HasColumnName("ViewClassGroupID");
            entity.Property(e => e.ClassId).HasColumnName("ClassID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ViewDateTime).HasColumnType("datetime");

            entity.HasOne(d => d.Class).WithMany(p => p.ViewClassGroups)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ViewClassGroup_ClassGroups");

            entity.HasOne(d => d.User).WithMany(p => p.ViewClassGroups)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ViewClassGroup_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
