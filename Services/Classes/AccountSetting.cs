using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Classes
{
    /// <summary>
    /// اطلاعات اکانت سیستم
    /// </summary>
    public class AccountSetting
    {
        public const string SectionName  = "AppSettings";

        public long SystemId { get; set; } //ایدی سیستم که برای ارسال انبوه استفاده میشه

        public string SchoolName { get; set; }

        public string SystemChatIcon { get; set; }
    }
}
