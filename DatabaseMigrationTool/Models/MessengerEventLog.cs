using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class MessengerEventLog
{
    public long MessengerLogId { get; set; }

    public DateTime CreateDate { get; set; }

    public string? Comment { get; set; }
}
