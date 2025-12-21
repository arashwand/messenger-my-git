using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    /// <summary>
    ///  برای نمایش پیام چت.
    /// </summary>
    public class ChatMessageDto
    {
        public long MessageId { get; set; }
        public string? Text { get; set; }
        public long SenderId { get; set; }
        public string? SenderName { get; set; }
        public DateTime SentAt { get; set; }
    }
}
