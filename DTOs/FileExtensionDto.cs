namespace Messenger.DTOs;

public class FileExtensionDto
{
    public int FileExtensionId { get; set; }
    public string? Extension { get; set; }
    public string? FontAwesome { get; set; } // Assuming this is for UI icons
    public string? Type { get; set; }
    public string? Comment { get; set; }
}
