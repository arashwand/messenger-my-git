using Messenger.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace Messenger.Services
{
    /// <summary>
    /// سرویسی برای مدیریت ذخیره و بازیابی آخرین پیام چت‌ها در Redis.
    /// اخرین پیام ارسالی را نگهداری میکنه برای اینکه در لیست چتها به کاربران نشون بده و بروزرسانی انجام بشه
    /// </summary>
    public class RedisLastMessageService
    {
        private readonly IDatabase _redisDb;
        private const string LastMessageKeyPrefix = "last_message";

        public RedisLastMessageService(IConnectionMultiplexer connectionMultiplexer)
        {
            // از طریق Dependency Injection، نمونه ConnectionMultiplexer را دریافت می‌کنیم.
            _redisDb = connectionMultiplexer.GetDatabase();
        }

        /// <summary>
        /// یک کلید منحصر به فرد برای هر چت بر اساس نوع و شناسه آن تولید می‌کند.
        /// مثال: last_message:group:123
        /// </summary>
        private string GenerateKey(string chatType, string id)
        {
            // این الگو تضمین می‌کند که کلیدها برای انواع مختلف چت با شناسه یکسان، تداخل نخواهند داشت.
            return $"{LastMessageKeyPrefix}:{chatType}:{id}";
        }

        /// <summary>
        /// آخرین پیام یک چت را در Redis ذخیره (یا به روز) می‌کند.
        /// این متد باید پس از هر بار ارسال پیام موفق فراخوانی شود.
        /// </summary>
        /// <param name="chatType">نوع چت ("group", "channel", "private")</param>
        /// <param name="id">شناسه چت</param>
        /// <param name="message">آبجکت پیام برای ذخیره</param>
        public async Task SetLastMessageAsync(string chatType, string id, ChatMessageDto message)
        {
            var key = GenerateKey(chatType, id);
            var serializedMessage = JsonSerializer.Serialize(message);
            await _redisDb.StringSetAsync(key, serializedMessage);
        }

        /// <summary>
        /// آخرین پیام یک چت را از Redis بازیابی می‌کند.
        /// </summary>
        /// <param name="chatType">نوع چت ("group", "channel", "private")</param>
        /// <param name="id">شناسه چت</param>
        /// <returns>آبجکت آخرین پیام یا null اگر وجود نداشته باشد.</returns>
        public async Task<ChatMessageDto?> GetLastMessageAsync(string chatType, string id)
        {
            var key = GenerateKey(chatType, id);
            var serializedMessage = await _redisDb.StringGetAsync(key);

            if (serializedMessage.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<ChatMessageDto>(serializedMessage!);
        }

        /// <summary>
        /// آخرین پیام‌ها را برای لیستی از چت‌ها به صورت یکجا از Redis بازیابی می‌کند.
        /// این متد برای زمانی که کاربر لاگین می‌کند و لیست چت‌هایش لود می‌شود، بسیار کارآمد است.
        /// </summary>
        /// <param name="chatIdentifiers">لیستی از شناسه‌های چت</param>
        /// <returns>یک دیکشنری که کلید آن شناسه ترکیبی چت و مقدار آن آخرین پیام است.</returns>
        public async Task<Dictionary<string, ChatMessageDto?>> GetLastMessagesAsync(IEnumerable<ChatIdentifier> chatIdentifiers)
        {
            var results = new Dictionary<string, ChatMessageDto?>();
            var batch = _redisDb.CreateBatch();

            // از دیکشنری برای نگاشت تسک به شناسه ساده استفاده می‌کنیم
            var taskMap = new Dictionary<Task<RedisValue>, string>();

            foreach (var identifier in chatIdentifiers)
            {
                var simpleId = $"{identifier.ChatType}:{identifier.Id}";
                // جلوگیری از ارسال درخواست تکراری برای یک چت
                if (taskMap.ContainsValue(simpleId)) continue;

                var key = GenerateKey(identifier.ChatType, identifier.Id); // متد private داخلی همچنان استفاده می‌شود
                var task = batch.StringGetAsync(key);
                taskMap.Add(task, simpleId);
            }

            batch.Execute();

            foreach (var pair in taskMap)
            {
                var task = pair.Key;
                var simpleId = pair.Value;
                var redisValue = await task;

                if (!redisValue.IsNullOrEmpty)
                {
                    results[simpleId] = JsonSerializer.Deserialize<ChatMessageDto>(redisValue!);
                }
                else
                {
                    results[simpleId] = null;
                }
            }

            return results;
        }
    }
}
