using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class Channel
{
    public int ChannelId { get; set; }

    public DateTime CreateDate { get; set; }

    public string ChannelName { get; set; } = null!;

    public string ChannelTitle { get; set; } = null!;

    public long CreatorUserId { get; set; }

    public virtual ICollection<ChannelMember> ChannelMembers { get; set; } = new List<ChannelMember>();

    public virtual ICollection<ChannelMessage> ChannelMessages { get; set; } = new List<ChannelMessage>();

    public virtual User CreatorUser { get; set; } = null!;
}
