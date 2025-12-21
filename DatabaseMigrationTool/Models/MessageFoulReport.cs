using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class MessageFoulReport
{
    public long MessageFoulReportId { get; set; }

    public DateTime FoulReportDateTime { get; set; }

    public long MessageId { get; set; }

    public long FoulReporterUserId { get; set; }

    public string? FoulDesc { get; set; }

    public virtual User FoulReporterUser { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}
