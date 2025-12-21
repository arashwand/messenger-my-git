using Messenger.Services.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.API.ServiceHelper
{
    /// <summary>
    /// نگهداری آخرین پیام خوانده شده هر کاربر در هر گرو یا کانال چت
    /// </summary>
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase _db;
        private readonly IServer _server;
        private const string LastReadKeyPrefix = "lastread:";

        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
            // برای استفاده از دستور SCAN به یک سرور نیاز داریم
            _server = redis.GetServer(redis.GetEndPoints().First());
        }

        public async Task SetLastReadMessageIdAsync(long userId, int groupId, long messageId)
        {
            var key = $"{LastReadKeyPrefix}{userId}:{groupId}";
            await _db.StringSetAsync(key, messageId.ToString());
        }

        public async Task<IEnumerable<string>> GetAllLastReadKeysAsync()
        {
            var keys = new List<string>();
            // از دستور SCAN برای پیدا کردن کلیدها بدون بلاک کردن سرور Redis استفاده می‌کنیم
            await foreach (var key in _server.KeysAsync(pattern: $"{LastReadKeyPrefix}*"))
            {
                keys.Add(key.ToString());
            }
            return keys;
        }

        public async Task<Dictionary<string, string>> GetValuesForKeysAsync(IEnumerable<string> keys)
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var redisValues = await _db.StringGetAsync(redisKeys);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < redisKeys.Length; i++)
            {
                if (!redisValues[i].IsNullOrEmpty)
                {
                    result[redisKeys[i]] = redisValues[i];
                }
            }
            return result;
        }

        public async Task DeleteKeysAsync(IEnumerable<string> keys)
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            if (redisKeys.Any())
            {
                await _db.KeyDeleteAsync(redisKeys);
            }
        }
    }
}
