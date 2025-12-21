namespace Messenger.DTOs;

public class MemberDto
{
    public long UserId { get; set; }

    /// <summary>
    /// channelId or classGroupId
    /// </summary>
    public long ClassId { get; set; }
    public bool IsMember { get; set; }
}
