namespace Messenger.DTOs;

using System;

public class PrivateChatItemDto
{
    public long ConversationId { get; set; }
    public long ChatId { get; set; }
    public string ChatKey { get; set; } = string.Empty;
    public string ChatName { get; set; } = string.Empty;
    public string? ProfilePicName { get; set; }
    public ChatMessageDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
    public bool IsSystemChat { get; set; }
    public long? OtherUserId { get; set; }
    public DateTime? LastMessageDateTime { get; set; }
}
