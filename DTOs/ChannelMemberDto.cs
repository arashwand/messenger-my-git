namespace Messenger.DTOs;

public class ChannelMemberDto
{
    public int ChannelMemberId { get; set; }
    public DateTime CreateDate { get; set; }
    public int ChannelId { get; set; }
    public int UserId { get; set; }

    // public ChannelDto Channel { get; set; }
    // public UserDto User { get; set; }
}
