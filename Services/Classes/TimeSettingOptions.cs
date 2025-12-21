using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Classes
{
    public class TimeSettingOptions
    {
        public const string SectionName = "TimeSetting";
        public int TimeToDeleteMessagesInMinutes { get; set; }
        public int TimeToEditMessagesInMinutes { get; set; }
    }
}
