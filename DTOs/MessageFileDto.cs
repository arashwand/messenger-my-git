namespace Messenger.DTOs;

public class MessageFileDto
{
    public long MessageFileId { get; set; }
    public long MessageId { get; set; }
    public DateTime CreateDate { get; set; }

    /// <summary>
    /// نام فایلی که کاربر ارسال کرده است
    /// </summary>
    public string? OriginalFileName { get; set; }
    public string? FileName { get; set; }
   // public int FileExtensionId { get; set; }
    public long FileSize { get; set; } // Assuming FileSize can be large
    public string? FilePath { get; set; }
    public string? FileThumbPath { get; set; }
    public string? FileType { get; set; }


    public double Duration { get; set; }
    public string DurationFormatted { get; set; }

    public FileExtensionDto FileExtension { get; set; }
}

