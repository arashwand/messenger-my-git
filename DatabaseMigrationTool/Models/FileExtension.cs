using System;
using System.Collections.Generic;

namespace DatabaseMigrationTool.Models;

public partial class FileExtension
{
    public int FileExtensionId { get; set; }

    public string Extension { get; set; } = null!;

    public string? FontAwesome { get; set; }

    public string? Type { get; set; }

    public string? Comment { get; set; }

    public virtual ICollection<MessageFile> MessageFiles { get; set; } = new List<MessageFile>();
}
