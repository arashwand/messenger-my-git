using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class ViewClassGroup
{
    public long ViewClassGroupId { get; set; }

    public DateTime ViewDateTime { get; set; }

    public long UserId { get; set; }

    public int ClassId { get; set; }

    public virtual ClassGroup Class { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
