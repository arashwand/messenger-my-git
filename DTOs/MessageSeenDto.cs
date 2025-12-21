using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class MessageSeenDto
    {
        public long MessageId { get; set; }
        public long TargetId { get; set; }
        public string GroupType { get; set; }
        public List<long> SeenUserIds { get; set; } = new List<long>();
    }

}
