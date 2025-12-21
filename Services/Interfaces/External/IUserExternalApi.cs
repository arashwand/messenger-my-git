using Messenger.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces.External
{
    public interface IUserExternalApi
    {
        /// <summary>
        /// توسط ایدی کاربر ، مشخصات او را از سرویس خارجی دریافت می‌کند
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<ResponseUserinfoDto?> GetUserByIdAsync(long userId);
    }
}
