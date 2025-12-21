using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class MessageRead
{
    public long ReadMessageId { get; set; }

    public long MessageId { get; set; }

    public long UserId { get; set; }

    public DateTime ReadDateTime { get; set; }

    /// <summary>
    /// ClassGroup = group
    /// ChannelGroup = channel
    /// Private = private chat
    /// </summary>
    public string GroupType { get; set; } = null!;

    /// <summary>
    /// ایدی چت مورد نظر - گروه یا کانال 
    /// </summary>
    public long TargetId { get; set; }

    public virtual Message Message { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
