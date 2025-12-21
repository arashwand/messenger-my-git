using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class PinMessageRequestDto
    {
        public long MessageId { get; set; }
        public bool IsPinned { get; set; }
    }
}
