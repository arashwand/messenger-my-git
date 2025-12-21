
namespace Messenger.API.ServiceHelper.Interfaces
{
    public interface IRedisUserStatusService
    {
        /// <summary>
        /// کاربر را در گروه مشخص به عنوان آنلاین ثبت می‌کند.
        /// </summary>
        Task SetUserOnlineAsync(string groupKey, long userId);

        /// <summary>
        /// وضعیت آنلاین بودن کاربر را از گروه مشخص حذف می‌کند.
        /// </summary>
        Task SetUserOfflineAsync(string groupKey, long userId);

        /// <summary>
        /// لیستی از شناسه کاربران آنلاین در گروه مشخص را برمی‌گرداند.
        /// </summary>
        Task<List<long>> GetOnlineUsersAsync(string groupKey);


        Task<string[]> GetUserGroupsAsync(long userId);
        Task<string[]> GetUserGroupKeysAsync(long userId);

        Task CacheUserGroupKeysAsync(long userId, IEnumerable<string> groupKeys);

    }
}
