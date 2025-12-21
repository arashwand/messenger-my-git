using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class PushDto
    {
    }

    /// <summary>
    /// مدل داده برای اشتراک پوش نوتیفیکیشن
    /// </summary>
    public class PushSubscriptionDto
    {
        public string Endpoint { get; set; }
        public KeysDto Keys { get; set; }
    }

    /// <summary>
    /// مدل داده برای کلیدهای اشتراک
    /// </summary>
    public class KeysDto
    {
        public string P256dh { get; set; }
        public string Auth { get; set; }
    }

    /// <summary>
    /// مدل داده برای لغو اشتراک پوش نوتیفیکیشن
    /// </summary>
    public class UnsubscribeDto
    {
        public string Endpoint { get; set; }
    }
}
