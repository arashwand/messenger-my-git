using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Tools
{
    public enum EnumMemberRoleType
    {
        /// <summary>
        /// عضو
        /// </summary>
        member,

        /// <summary>
        /// مدیر گروه یا کانال چت
        /// </summary>
        admin,
        
        /// <summary>
        /// مالک گروه یا کانال چت
        /// </summary>
        owner 
    }
}
