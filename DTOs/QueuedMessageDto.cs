namespace Messenger.DTOs
{
    /// <summary>
    /// DTO برای پیامهایی که در صف Hangfire قرار میگیرند
    /// </summary>
    public class QueuedMessageDto
    {
        /// <summary>
        /// شناسه کاربر ارسال کننده
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// شناسه گروه یا کانال
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// نوع گروه (ClassGroup یا ChannelGroup)
        /// </summary>
        public string GroupType { get; set; } = string.Empty;

        /// <summary>
        /// متن پیام
        /// </summary>
        public string MessageText { get; set; } = string.Empty;

        /// <summary>
        /// لیست شناسه فایلهای پیوست
        /// </summary>
        public List<long>? FileAttachementIds { get; set; }

        /// <summary>
        /// شناسه پیام مرجع (در صورت پاسخ به پیام)
        /// </summary>
        public long? ReplyToMessageId { get; set; }

        /// <summary>
        /// شناسه منحصربفرد پیام در سمت کلاینت
        /// </summary>
        public string? ClientMessageId { get; set; }

        /// <summary>
        /// زمان اضافه شدن به صف
        /// </summary>
        public DateTime QueuedAt { get; set; }

        /// <summary>
        /// اولویت پیام
        /// </summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    }
}
