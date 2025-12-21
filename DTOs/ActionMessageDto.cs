using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class ActionMessageDto
    {
        public long UserId { get; set; }
        public long MessageId { get; set; }
        public long ClassGroupId { get; set; }
        public string GroupType { get; set; }
        public byte MessageType { get; set; }
    }
}
