using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class UserClassGroup
{
    public long UserClassGroupId { get; set; }

    public long UserId { get; set; }

    public int ClassId { get; set; }

    public long? LastReadMessageId { get; set; }

    public byte MemberRoleType { get; set; }

    public virtual ClassGroup Class { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
