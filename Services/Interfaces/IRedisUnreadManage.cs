using Messenger.DTOs;
using Messenger.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IRedisUnreadManage
    {
        Task IncrementUnreadCountAsync(long userId, long targetId, string groupType);
        Task DecrementUnreadCountAsync(long userId, long targetId, string groupType);
        Task ResetUnreadCountAsync(long userId, long targetId, string groupType);
        //Task MarkMessageAsReadAsync(long userId, long messageId);
        //Task MarkMessageAsUnreadAsync(long userId, long messageId);
        //Task<bool> IsMessageUnreadAsync(long userId, long messageId);
        Task<Dictionary<string, int>> GetUnreadCountsAsync(long userId, IEnumerable<int> groupIds, IEnumerable<int> channelIds);
        //Task<Dictionary<int, bool>> GetMessageStatusesAsync(int userId);
        //Task<int> GetUnreadMessageCountForChatAsync(long userId, long targetId, string groupType, IEMessengerDbContext context);

        //Task<List<UnreadMessageDto>> GetUnreadMessageIdsForChatAsync(long userId, long targetId, string groupType, IEMessengerDbContext context); // New method

        Task<int> GetUnreadCountAsync(long userId, long targetId, string groupType);

        Task SetUnreadCountAsync(long userId, long targetId, string groupType, int count);

        Task SetLastReadMessageIdAsync(long userId, long targetId, string groupType, long messageId);
        Task<long> GetLastReadMessageIdAsync(long userId, long targetId, string groupType);

        #region new Feature
        Task MarkMessageAsSeenAsync(long userId, long messageId, long targetId, string groupType);
        Task<long> GetMessageSeenCountAsync(long messageId);
        Task<List<long>> GetUsersWhoSeenMessageAsync(long messageId);

        /// <summary>
        /// تمام کلیدهای Redis مرتبط با مدیریت پیام‌های خوانده شده/نشده چت را حذف می‌کند.
        /// این متد فقط برای استفاده در محیط‌های تست و توسعه/ابزارهای مدیریت کاربرد دارد.
        /// </summary>
        Task<int> ClearAllChatKeysAsync();

        /// <summary>
        /// تمام کلیدها و مقادیر Redis مرتبط با یک کاربر خاص را برای اهداف گزارش‌گیری و عیب‌یابی برمی‌گرداند.
        /// </summary>
        Task<Dictionary<string, string>> GetUserStatusReportAsync(long userId);

        #endregion

    }
}
