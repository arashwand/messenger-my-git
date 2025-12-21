using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.Text.Json;

namespace Messenger.WebApp.ServiceHelper
{
    using Messenger.Moderation;
    using StackExchange.Redis;
    using System.Text.Json;

    public class RedisWordManager
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ProfanityFilter _profanityFilter;
        private readonly string _seedFilePath; // مسیر فایل json
        private const string REDIS_KEY = "chat:badwords";
        private const string CHANNEL_NAME = "chat:badwords:update";

        public RedisWordManager(IConnectionMultiplexer redis, ProfanityFilter profanityFilter, IWebHostEnvironment env)
        {
            _redis = redis;
            _profanityFilter = profanityFilter;
            _seedFilePath = Path.Combine(env.ContentRootPath, "Data", "badwords.json");
        }

        public async Task InitializeAsync()
        {
            var db = _redis.GetDatabase();
            var sub = _redis.GetSubscriber();

            // 1. چک می‌کنیم آیا در Redis دیتایی داریم؟
            bool exists = await db.KeyExistsAsync(REDIS_KEY);

            if (!exists)
            {
                // اگر Redis خالی بود، عملیات Seeding را انجام بده
                await SeedFromLocalFileAsync(db);
            }

            // 2. حالا که مطمئن شدیم دیتا هست (یا بود، یا ریختیم)، آن را می‌خوانیم
            var words = await GetWordsFromRedisAsync();

            // 3. درخت Trie را می‌سازیم
            _profanityFilter.Reload(words);

            // 4. به تغییرات گوش می‌دهیم (Pub/Sub)
            await sub.SubscribeAsync(CHANNEL_NAME, async (channel, message) =>
            {
                var updatedWords = await GetWordsFromRedisAsync();
                _profanityFilter.Reload(updatedWords);
            });
        }

        // متد اختصاصی برای خواندن فایل و ریختن در Redis
        public async Task SeedFromLocalFileAsync(IDatabase db = null)
        {
            if (db == null) db = _redis.GetDatabase();

            if (File.Exists(_seedFilePath))
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(_seedFilePath);
                    var wordsList = JsonSerializer.Deserialize<List<string>>(jsonContent);

                    if (wordsList != null && wordsList.Any())
                    {
                        // تبدیل لیست رشته به آرایه‌ای از RedisValue برای سرعت بالا
                        // نکته مهم: از Batch Insert استفاده می‌کنیم نه حلقه for
                        var redisValues = wordsList
                                          .Where(w => !string.IsNullOrWhiteSpace(w))
                                          .Select(w => (RedisValue)w.Trim())
                                          .ToArray();

                        if (redisValues.Length > 0)
                        {
                            // تمام کلمات را یکجا به Set اضافه می‌کند
                            await db.SetAddAsync(REDIS_KEY, redisValues);
                            Console.WriteLine($"[Redis Seed] Successfully imported {redisValues.Length} words from JSON.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Redis Seed Error] Could not seed data: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[Redis Seed] JSON file not found. Skipping seed.");
            }
        }

        // ... (بقیه متدها مثل AddWordAsync و GetWordsFromRedisAsync مثل قبل باقی می‌مانند)

        public async Task<List<string>> GetWordsFromRedisAsync()
        {
            var db = _redis.GetDatabase();
            var redisValues = await db.SetMembersAsync(REDIS_KEY);
            return redisValues.Select(v => v.ToString()).ToList();
        }

        // متد اضافه کردن تکی (جهت یادآوری)
        public async Task AddWordAsync(string word)
        {
            var db = _redis.GetDatabase();
            await db.SetAddAsync(REDIS_KEY, word.Trim());
            await _redis.GetSubscriber().PublishAsync(CHANNEL_NAME, "updated");
        }
        public async Task RemoveWordAsync(string word)
        {
            var db = _redis.GetDatabase();
            await db.SetRemoveAsync(REDIS_KEY, word.Trim());
            await _redis.GetSubscriber().PublishAsync(CHANNEL_NAME, "updated");
        }


        public async Task ForceImportJsonAsync()
        {
            var db = _redis.GetDatabase();
            var sub = _redis.GetSubscriber();

            // 1. خواندن فایل و ریختن در Redis (همان متدی که قبلاً ساختیم)
            await SeedFromLocalFileAsync(db);

            // 2. خبر دادن به تمام Instanceها (از جمله همین سرور) برای ریلود کردن رم
            // پیام "imported" ارسال می‌شود تا همه بدانند تغییری رخ داده
            await sub.PublishAsync(CHANNEL_NAME, "imported");

            Console.WriteLine("[Redis] Force import completed and notification sent.");
        }
    }
}
