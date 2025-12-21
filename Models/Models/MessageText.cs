using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class MessageText
{
    public long MessageTextId { get; set; }

    public long MessageId { get; set; }

    public string MessageTxt { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}
