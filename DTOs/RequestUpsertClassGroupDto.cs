using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class RequestUpsertClassGroupDto
    {
        public long ClassId { get; set; }
        public string? LevelName { get; set; }
        public long TeacherUserId { get; set; } = 0;
        public string? ClassTiming { get; set; }
        public bool IsActive { get; set; }
        public DateTime EndDate { get; set; }
    }
}
