using Messenger.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IManageUserService
    {
        /// <summary>
        /// دریافت تمام کاربران
        /// </summary>
        Task<List<UserDto>> GetAllUsersAsync();

        /// <summary>
        /// دریافت کاربر بر اساس شناسه
        /// </summary>
        Task<UserDto?> GetUserByIdAsync(long userId);

        /// <summary>
        /// ایجاد کاربر جدید
        /// </summary>
        Task CreateUserAsync(UserDto user);

        /// <summary>
        /// بروزرسانی اطلاعات کاربر
        /// </summary>
        Task UpdateUserAsync(UserDto user);

        /// <summary>
        /// حذف کاربر
        /// </summary>
        Task DeleteUserAsync(long userId);
    }

}

