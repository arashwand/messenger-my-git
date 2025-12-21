using Azure.Core;
using Messenger.API.ServiceHelper.Interfaces;
using StackExchange.Redis;
using System.Net.Http;

namespace Messenger.API.Services
{
    public class RedisUserStatusService : IRedisUserStatusService
    {
        private readonly IDatabase _db;
        private const int TTL_SECONDS = 5 * 60; // زمان زنده ماندن کلید

        // زمان انقضا برای کش گروه‌های کاربر (مثلاً ۲۴ ساعت)
        private const int USER_GROUPS_CACHE_TTL_SECONDS = 86400;

        public RedisUserStatusService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private string GetRedisKey(string groupKey)
        {
            return $"online:{groupKey}";
        }

        public async Task SetUserOnlineAsync(string groupKey, long userId)
        {
            var key = GetRedisKey(groupKey);
            await _db.SetAddAsync(key, userId);
            // تمدید زمان انقضای کلید با هر فعالیت
            await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(TTL_SECONDS));
        }

        public async Task SetUserOfflineAsync(string groupKey, long userId)
        {
            var key = GetRedisKey(groupKey);
            await _db.SetRemoveAsync(key, userId);
        }

        public async Task<List<long>> GetOnlineUsersAsync(string groupKey)
        {
            var key = GetRedisKey(groupKey);
            var members = await _db.SetMembersAsync(key);
            return members.Select(m => (long)m).ToList();
        }

        // متد جدید برای دریافت گروه‌های کاربر
        public async Task<string[]> GetUserGroupsAsync(long userId)
        {
            var key = $"user:{userId}:groups";
            var members = await _db.SetMembersAsync(key);
            return members.Select(m => (string)m).ToArray();
        }

        /// <summary>
        /// Retrieves the cached list of group keys for a specific user from Redis.
        /// This is the CORRECT and EFFICIENT way.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>An array of group key strings.</returns>
        public async Task<string[]> GetUserGroupKeysAsync(long userId)
        {
            // 1. Define the specific key for the user's groups. No scanning needed!
            var userGroupsKey = $"user:{userId}:groups";

            // 2. Fetch all members of the set directly using SMEMBERS command. This is very fast.
            var members = await _db.SetMembersAsync(userGroupsKey);

            // 3. Convert the result to a string array.
            return members.Select(m => m.ToString()).ToArray();
        }

        public async Task CacheUserGroupKeysAsync(long userId, IEnumerable<string> groupKeys)
        {
            // ۱. ساخت کلید منحصر به فرد برای نگهداری گروه‌های این کاربر
            var userGroupsKey = $"user:{userId}:groups";

            // اگر لیست ورودی خالی است، کاری انجام نده
            if (groupKeys == null || !groupKeys.Any())
            {
                return;
            }

            // ۲. تبدیل لیست رشته‌ها به RedisValue[] برای افزودن یکباره
            var redisValues = groupKeys.Select(gk => (RedisValue)gk).ToArray();

            // ۳. افزودن تمام کلیدهای گروه به Set 
            await _db.SetAddAsync(userGroupsKey, redisValues);

            // ۴. تنظیم زمان انقضا برای این کلید.
            // این کار مهم است تا اگر عضویت کاربر در گروه‌ها تغییر کرد، کش پس از مدتی باطل شود و دوباره از دیتابیس خوانده شود.
            await _db.KeyExpireAsync(userGroupsKey, TimeSpan.FromSeconds(USER_GROUPS_CACHE_TTL_SECONDS));
        }

    }
}
