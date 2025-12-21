using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class ClassGroupMessagesView
{
    public long MessageId { get; set; }

    public long? ReplyMessageId { get; set; }

    public DateTime MessageDateTime { get; set; }

    public long SenderUserId { get; set; }

    public bool IsPin { get; set; }

    public bool IsHidden { get; set; }

    public bool IsPrivate { get; set; }

    public long ClassId { get; set; }

    public string LevelName { get; set; } = null!;

    public long TeacherUserId { get; set; }

    public string ClassTiming { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? LeftSes { get; set; }

    public DateTime? EndDate { get; set; }
}
