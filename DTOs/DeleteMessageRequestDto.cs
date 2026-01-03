namespace Messenger.DTOs
{
    public class DeleteMessageRequestDto
    {
        public long GroupId { get; set; }

        /// <summary>
        /// 0 - Group Chat
        /// 1 - Channel Chat
        /// 2 - Private Chat
        /// </summary>
        public string GroupType { get; set; }

        public long MessageId { get; set; }

        /// <summary>
        /// اگر پیام از سمت پرتال ارسال شده باشد این مقدار true خواهد بود
        /// </summary>
        public bool IsPortalMessage { get; set; } = false;
    }
}
