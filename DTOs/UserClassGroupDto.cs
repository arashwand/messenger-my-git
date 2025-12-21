namespace Messenger.DTOs;

public class UserClassGroupDto
{
    public int UserClassGroupId { get; set; }
    public long UserId { get; set; }
    public int ClassId { get; set; }
    public long LastReadMessageId { get; set; }

    // public UserDto User { get; set; }
    // public ClassGroupDto ClassGroup { get; set; }
}
