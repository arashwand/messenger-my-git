namespace Messenger.DTOs;

public class MessengerEventLogDto
{
    public int MessengerLogId { get; set; }
    public DateTime CreateDate { get; set; }
    public string? Comment { get; set; }
}
