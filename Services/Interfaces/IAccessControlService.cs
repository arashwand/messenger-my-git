using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    /// <summary>
    /// سرویس کنترل دسترسی - بررسی می‌کند که آیا کاربر بر اساس نقش خود به منابع دسترسی دارد یا خیر
    /// </summary>
    public interface IAccessControlService
    {
        /// <summary>
        /// بررسی می‌کند که آیا کاربر دسترسی به گروه کلاسی دارد یا خیر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="classGroupId">شناسه گروه کلاسی</param>
        /// <returns>اگر کاربر دسترسی داشته باشد true برمی‌گرداند</returns>
        Task<bool> CanAccessClassGroupAsync(long userId, long classGroupId);

        /// <summary>
        /// بررسی می‌کند که آیا کاربر می‌تواند گروه کلاسی را ویرایش کند یا خیر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="classGroupId">شناسه گروه کلاسی</param>
        /// <returns>اگر کاربر دسترسی ویرایش داشته باشد true برمی‌گرداند</returns>
        Task<bool> CanEditClassGroupAsync(long userId, long classGroupId);

        /// <summary>
        /// بررسی می‌کند که آیا کاربر می‌تواند اعضا را مدیریت کند یا خیر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="classGroupId">شناسه گروه کلاسی</param>
        /// <returns>اگر کاربر دسترسی مدیریت اعضا داشته باشد true برمی‌گرداند</returns>
        Task<bool> CanManageClassGroupMembersAsync(long userId, long classGroupId);

        /// <summary>
        /// بررسی می‌کند که آیا کاربر نقش مدیر یا پرسنل دارد یا خیر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <returns>اگر کاربر نقش مدیریتی داشته باشد true برمی‌گرداند</returns>
        Task<bool> IsAdminOrPersonelAsync(long userId);

        /// <summary>
        /// بررسی می‌کند که آیا کاربر معلم این گروه کلاسی است یا خیر
        /// </summary>
        /// <param name="userId">شناسه کاربر</param>
        /// <param name="classGroupId">شناسه گروه کلاسی</param>
        /// <returns>اگر کاربر معلم گروه باشد true برمی‌گرداند</returns>
        Task<bool> IsTeacherOfClassGroupAsync(long userId, long classGroupId);
    }
}