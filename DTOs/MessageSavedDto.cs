namespace Messenger.DTOs;

public class MessageSavedDto
{
    public long MessageSavedId { get; set; }
    public long MessageId { get; set; }
    public DateTime SaveDateTime { get; set; }
    public long UserId { get; set; }

     public MessageDto Message { get; set; }
    // public UserDto User { get; set; }
}
