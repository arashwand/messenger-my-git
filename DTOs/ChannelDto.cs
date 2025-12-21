namespace Messenger.DTOs;

public class ChannelDto
{
    public long ChannelId { get; set; }
    public DateTime CreateDate { get; set; }
    public string? ChannelName { get; set; }
    public string? ChannelTitle { get; set; }
    public long? CreatorUserId { get; set; }
    public double LastReadMessageId { get; set; } = 0;

    public ChatMessageDto? LastMessage { get; set; } = null;
    public int UnreadCount { get; set; }

    // public ICollection<ChannelMemberDto> ChannelMembers { get; set; }
    // public ICollection<ChannelMessageDto> ChannelMessages { get; set; }
}
