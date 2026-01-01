using System;

namespace Messenger.Models.Models;

public partial class MessageRecipient
{
    public long MessageRecipientId { get; set; }

    public long MessageId { get; set; }

    public long RecipientUserId { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime? ReadDateTime { get; set; }

    public virtual Message Message { get; set; } = null!;

    public virtual PrivateChatConversation Conversation { get; set; } = null!; 



}
