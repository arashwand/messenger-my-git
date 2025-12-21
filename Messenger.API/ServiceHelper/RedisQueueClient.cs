using Messenger.API.ServiceHelper.Interfaces;
using StackExchange.Redis;

namespace Messenger.API.ServiceHelper
{
    public class RedisQueueClient : IRedisQueueClient
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisQueueClient(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task EnqueueAsync(string queueName, string message)
        {
            var db = _redis.GetDatabase();
            // LPUSH برای اضافه کردن به ابتدای صف (یا RPUSH بسته به سیاست)
            await db.ListLeftPushAsync(queueName, message);
        }

        public async Task<string?> DequeueAsync(string queueName, CancellationToken cancellationToken)
        {
            var db = _redis.GetDatabase();
            // RPOP برای دریافت از انتها (یک مدل FIFO)
            RedisValue value;
            while (!cancellationToken.IsCancellationRequested)
            {
                value = await db.ListRightPopAsync(queueName);
                if (!value.IsNull)
                    return value;
                // اگر صف خالی است، کمی صبر کن
                await Task.Delay(500, cancellationToken);
            }
            return null;
        }
    }
}
