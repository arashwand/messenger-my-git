using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public enum EnumMessageType
    {
        /// <summary>
        /// گروه
        /// </summary>
        [Display(Name = "گروه")]
        Group,

        /// <summary>
        /// کانال
        /// </summary>
        [Display(Name = "کانال")]
        Channel,

        /// <summary>
        /// چت خصوصی
        /// </summary>
        [Display(Name = "چت خصوصی")]
        Private,


        /// <summary>
        /// همه گروهها
        /// </summary>
        [Display(Name = "همه گروهها ها")]
        AllGroups,


        /// <summary>
        /// همه کانالها
        /// </summary>
        [Display(Name = "همه کانالها")]
        AllChannels,

        /// <summary>
        /// همه دانشجوها
        /// </summary>
        [Display(Name = "همه دانشجوها")]
        AllStudents = 50,

        /// <summary>
        /// همه اساتید
        /// </summary>
        [Display(Name = "همه اساتید")]
        AllTeachers = 51,

        /// <summary>
        /// همه پرسنل
        /// </summary>
        [Display(Name = "همه پرسنل")]
        AllPersonel = 52
    }


}
