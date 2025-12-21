using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class BlockedUser
{
    public int BlockedUserId { get; set; }

    public DateTime BlockDate { get; set; }

    public long UserId { get; set; }

    public string? Comment { get; set; }

    public long CreatorUserId { get; set; }

    public virtual User User { get; set; } = null!;
}
