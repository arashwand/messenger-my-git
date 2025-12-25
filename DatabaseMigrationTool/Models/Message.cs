using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class Message
{
    public long MessageId { get; set; }

    public long? ReplyMessageId { get; set; }

    public DateTime MessageDateTime { get; set; }

    public DateTime? LastEditDateTime { get; set; }

    public long SenderUserId { get; set; }

    public bool IsPin { get; set; }

    public bool IsHidden { get; set; }

    public bool IsEdited { get; set; }

    public byte MessageType { get; set; }

    public bool IsSystemMessage { get; set; }

    public virtual ICollection<ChannelMessage> ChannelMessages { get; set; } = new List<ChannelMessage>();

    public virtual ICollection<ClassGroupMessage> ClassGroupMessages { get; set; } = new List<ClassGroupMessage>();

    public virtual ICollection<Message> InverseReplyMessage { get; set; } = new List<Message>();

    public virtual ICollection<MessageFile> MessageFiles { get; set; } = new List<MessageFile>();

    public virtual ICollection<MessageFoulReport> MessageFoulReports { get; set; } = new List<MessageFoulReport>();

    public virtual ICollection<MessageRead> MessageReads { get; set; } = new List<MessageRead>();

    public virtual ICollection<MessageSaved> MessageSaveds { get; set; } = new List<MessageSaved>();

    public virtual ICollection<MessageText> MessageTexts { get; set; } = new List<MessageText>();

    public virtual Message? ReplyMessage { get; set; }

    public virtual User SenderUser { get; set; } = null!;
}
