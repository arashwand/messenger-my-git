using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public static class GenerateSignalRGroupKey
    {
        /// <summary>
        /// ایدی کانال یا گروه میگیره و شناسه ایجاد میکنه ChannelGroup | ClassGroup
        /// </summary>
        /// <param name="id">ایدی کانال یا گروه چت</param>
        /// <param name="groupType">نوع : کانال یا گروه که با توجه به کلاس ثابت   ConstChat  فراخوانی میشه</param>
        /// <returns></returns>
        public static string GenerateKey(long id,string groupType)
        {
            return groupType == ConstChat.ClassGroupType
                ? ConstChat.ClassGroup + id
                : ConstChat.ChannelGroup + id;
        }
    }
}
