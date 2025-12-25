namespace Messenger.DTOs;

public class MessageDto
{
    public long MessageId { get; set; }        

    public DateTime MessageDateTime { get; set; }
    public long SenderUserId { get; set; }
    public bool IsPin { get; set; }
    public DateTime? PinnedAt { get; set; }
    public long? PinnedByUserId { get; set; }
    public bool IsSystemMessage { get; set; }
    
    public bool IsHidden { get; set; }

    /// <summary>
    /// نوع چت را مشخص میکند که گروه یا کانال یا خصوصی است
    /// 0: گروه
    /// 1: کانال
    /// 2: خصوصی
    /// </summary>
    public byte MessageType { get; set; }

    public bool IsEdited { get; set; }
    public bool IsReadByCurrentUser { get; set; }
    public string? ClientMessageId { get; set; }

    /// <summary>
    /// ایدی چت میباشد که این پیام متعلق به ان است
    /// مثال ایدی گرو یا کانال یا چت خصوصی
    /// بر اساس MessageType مشخص میشود
    /// </summary>
    public long OwnerId { get; set; }

    /// <summary>
    /// ایدی گیرنده پیام برای پیام‌های خصوصی
    /// </summary>
    public long? ReceiverUserId { get; set; }

    /// <summary>
    /// شناسه گروه/کاربر هدف برای routing در کلاینت
    /// - برای Group: ClassId
    /// - برای Channel: ChannelId  
    /// - برای Private: otherUserId (کاربر مقابل)
    /// </summary>
    public long GroupId { get; set; }
    
    /// <summary>
    /// نوع چت: ClassGroup, ChannelGroup, Private
    /// </summary>
    public string? GroupType { get; set; }

    // Navigation properties or related DTOs can be added later if needed
    public UserDto SenderUser { get; set; } = new UserDto();
    public long? ReplyMessageId { get; set; }
    public MessageDto? ReplyMessage { get; set; } // برای نمایش در UI
    public MessageTextDto? MessageText { get; set; }
    public IEnumerable<MessageFileDto>? MessageFiles { get; set; }
    public bool IsReadByAnyRecipient { get; set; }
    public int MessageSeenCount { get; set; }


}
