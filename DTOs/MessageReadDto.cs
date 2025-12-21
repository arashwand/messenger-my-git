namespace Messenger.DTOs;

public class MessageReadDto
{
    public long ReadMessageId { get; set; }
    public long MessageId { get; set; }
    public long UserId { get; set; }
    public DateTime ReadDateTime { get; set; }

    // public MessageDto Message { get; set; }
    // public UserDto User { get; set; }
}
