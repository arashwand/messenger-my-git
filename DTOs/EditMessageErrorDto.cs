using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class EditMessageErrorDto
    {
        public long UserId { get; set; }
        public long MessageId { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }
        public int? AllowedMinutes { get; set; }
    }
}
