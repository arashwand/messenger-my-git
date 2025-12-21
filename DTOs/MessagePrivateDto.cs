namespace Messenger.DTOs;

public class MessagePrivateDto
{
    public int MessagePrivateId { get; set; }
    public int MessageId { get; set; }
    public int GetterUserId { get; set; }

    // public MessageDto Message { get; set; }
    // public UserDto GetterUser { get; set; }
}
