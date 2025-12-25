using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class User
{
    public long UserId { get; set; }

    public string RoleName { get; set; } = null!;

    public string RoleFaName { get; set; } = null!;

    public string NameFamily { get; set; } = null!;

    public string? DeptName { get; set; }

    public string? ProfilePicName { get; set; }

    public string? LoginToken { get; set; }

    public virtual ICollection<BlockedUser> BlockedUsers { get; set; } = new List<BlockedUser>();

    public virtual ICollection<ChannelMember> ChannelMembers { get; set; } = new List<ChannelMember>();

    public virtual ICollection<Channel> Channels { get; set; } = new List<Channel>();

    public virtual ICollection<ClassGroup> ClassGroups { get; set; } = new List<ClassGroup>();

    public virtual ICollection<MessageFoulReport> MessageFoulReports { get; set; } = new List<MessageFoulReport>();

    public virtual ICollection<MessagePrivate> MessagePrivates { get; set; } = new List<MessagePrivate>();

    public virtual ICollection<MessageRead> MessageReads { get; set; } = new List<MessageRead>();

    public virtual ICollection<MessageRecipient> MessageRecipients { get; set; } = new List<MessageRecipient>();

    public virtual ICollection<MessageSaved> MessageSaveds { get; set; } = new List<MessageSaved>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<PersonelChatAccess> PersonelChatAccesses { get; set; } = new List<PersonelChatAccess>();

    public virtual ICollection<UserClassGroup> UserClassGroups { get; set; } = new List<UserClassGroup>();

    public virtual ICollection<ViewClassGroup> ViewClassGroups { get; set; } = new List<ViewClassGroup>();
}
