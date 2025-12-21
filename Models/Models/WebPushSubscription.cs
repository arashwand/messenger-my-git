using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Models.Models
{
    public class WebPushSubscription
    {
        public int Id { get; set; }
        public string UserId { get; set; }   // فرض Identity یا هر شناسه کاربر
        public string Endpoint { get; set; }
        public string P256dh { get; set; }
        public string Auth { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
