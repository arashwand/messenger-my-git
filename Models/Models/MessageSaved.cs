using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class MessageSaved
{
    public long MessageSavedId { get; set; }

    public long MessageId { get; set; }

    public DateTime SaveDateTime { get; set; }

    public long UserId { get; set; }

    public virtual Message Message { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
