using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class ChannelMember
{
    public long ChanelMemberId { get; set; }

    public DateTime CreateDate { get; set; }

    public int ChannelId { get; set; }

    public long UserId { get; set; }

    public byte MemberRoleType { get; set; }

    public virtual Channel Channel { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
