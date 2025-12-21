using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class ChannelMessage
{
    public long ChannelMessageId { get; set; }

    public int ChannelId { get; set; }

    public long MessageId { get; set; }

    public virtual Channel Channel { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}
