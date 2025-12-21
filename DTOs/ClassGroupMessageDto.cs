namespace Messenger.DTOs;

public class ClassGroupMessageDto // Renamed from ClassGroupMessages for convention
{
    public int ClassGroupMessageId { get; set; }
    public int ClassId { get; set; }
    public int MessageId { get; set; }

    // public ClassGroupDto ClassGroup { get; set; }
    // public MessageDto Message { get; set; }
}
