using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IRedisCacheService
    {
        /// <summary>
        /// شناسه آخرین پیام خوانده شده توسط کاربر در یک گروه را در Redis ثبت می‌کند.
        /// </summary>
        Task SetLastReadMessageIdAsync(long userId, int groupId, long messageId);

        /// <summary>
        /// لیستی از کلیدهای وضعیت خوانده شدن را برای همگام‌سازی باز می‌گرداند.
        /// </summary>
        Task<IEnumerable<string>> GetAllLastReadKeysAsync();

        /// <summary>
        /// مقادیر(شناسه پیام‌ها) را برای لیستی از کلیدها باز می‌گرداند.
        /// </summary>
        Task<Dictionary<string, string>> GetValuesForKeysAsync(IEnumerable<string> keys);

        /// <summary>
        /// کلیدهای مشخص شده را از Redis حذف می‌کند.
        /// </summary>
        Task DeleteKeysAsync(IEnumerable<string> keys);
    }
}
