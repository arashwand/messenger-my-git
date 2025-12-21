using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class SyncChatRequest
    {
        public string GroupType { get; set; }
        public string ChatId { get; set; }
        public List<string> ClientMessageIds { get; set; } = new();
        public DateTime SyncFrom { get; set; }
        public DateTime SyncTo { get; set; }
    }

    public class SyncChatResult
    {
        public List<long> DeletedMessageIds { get; set; } = new();
        public List<MessageDto> EditedMessages { get; set; } = new();
        public List<MessageDto> NewMessages { get; set; } = new();
    }

    public class SyncResultDto
    {
        public List<long> DeletedMessageIds { get; set; } = new();
        public List<MessageDto> EditedMessages { get; set; } = new();
        public List<MessageDto> NewMessages { get; set; } = new();
    }
}
