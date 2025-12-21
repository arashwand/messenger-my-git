using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class MessagePrivate
{
    public long MessagePrivateId { get; set; }

    public long MessageId { get; set; }

    public long? GetterUserId { get; set; }

    public virtual User? GetterUser { get; set; }

    public virtual Message Message { get; set; } = null!;
}
