using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class ClassGroupMessage
{
    public long ClassGroupMessageId { get; set; }

    public int ClassId { get; set; }

    public long MessageId { get; set; }

    public virtual ClassGroup Class { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}
