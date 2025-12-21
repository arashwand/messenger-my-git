using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class MessageFile
{
    public long MessageFileId { get; set; }

    public long? MessageId { get; set; }

    public DateTime CreateDate { get; set; }

    public string FileName { get; set; } = null!;

    public int FileExtensionId { get; set; }

    public long FileSize { get; set; }

    public long UploaderUserId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    /// <summary>
    /// if file type is Image, this filed has value
    /// </summary>
    public string? FileThumbPath { get; set; }

    public virtual FileExtension FileExtension { get; set; } = null!;

    public virtual Message? Message { get; set; }
}
