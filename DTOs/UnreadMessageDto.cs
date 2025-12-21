using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class UnreadMessageDto
    {
        public long SenderUserId { get; set; }
        public long MessageId { get; set; }
    }
}
