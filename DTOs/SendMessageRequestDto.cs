using Messenger.Tools;
using System.ComponentModel.DataAnnotations;

namespace Messenger.DTOs
{
    public class SendMessageRequestDto
    {
        public long UserId { get; set; }
        public long GroupId { get; set; }
        public string MessageText { get; set; }
        public string GroupType { get; set; }
        public long? ReplyToMessageId { get; set; }
        public List<long>? FileAttachementIds { get; set; }
        public string ClientMessageId { get; set; }
    }

    public class SendMessageFromPortalDto
    {
        [Required]
        public int TargetId { get; set; }

        [Required]
        public string GroupType { get; set; }

        [Required]
        public string MessageText { get; set; }
        public bool IsPin { get; set; } = true;
    }

    public class SendMessageToAllFromPortalDto
    {
        [Required]
        public EnumMessageType MessageType { get; set; }

        [Required]
        public string MessageText { get; set; }
        public bool IsPin { get; set; } = true;
    }

    /// <summary>
    /// DTO برای ارسال پیام به یک هدف خاص: فرد، گروه یا کانال
    /// </summary>
    public class SendMessageToTargetDto
    {
        [Required]
        public string TargetType { get; set; } // "Private", "Group", "Channel"

        [Required]
        public int TargetId { get; set; } // برای Private: receiverUserId, برای Group/Channel: id

        [Required]
        public string MessageText { get; set; }

        public bool IsPin { get; set; } = false;

        public List<long>? FileIds { get; set; }

        public bool IsPortalMessage { get; set; }
    }

    public class BroadcastResultDto
    {
        public string MessageText { get; set; }

        /// <summary>
        /// لیست ایدی افراد
        /// </summary>
        public int TargetIdsCount { get; set; }
    }
}
