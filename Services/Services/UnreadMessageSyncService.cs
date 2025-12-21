using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Messenger.Services.Services
{

    /// <summary>
    /// سرویسی که در بک گراند هر 5 دقیقه عمل میکند.
    /// وظیفه همسان‌سازی وضعیت "دیده شدن پیام" (Seen By Set) را از Redis به دیتابیس (SQL) بر عهده دارد.
    /// کاربران بیننده را به جدول MessageReads منتقل کرده و پس از پردازش، آنها را از Redis Set حذف می‌کند.
    /// </summary>
    public class UnreadMessageSyncService : IHostedService, IDisposable
    {
        private Timer _timer;
        private readonly ILogger<UnreadMessageSyncService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5); // هر 5 دقیقه یکبار

        public UnreadMessageSyncService(ILogger<UnreadMessageSyncService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Unread Message Sync Service starting.");

            //TODO  بعدا برای محیط واقعی باید خط زیر از کامنت خارج بشه تا بتونه در دیتابیس ذخیره کنه
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _syncInterval);
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            _logger.LogInformation($"Unread Message Sync Service is running to transfer 'Seen By' status from Redis to SQL at: {DateTime.Now}");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IEMessengerDbContext>();
                var redisDatabase = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

                try
                {
                    // 1. انتقال وضعیت "دیده شدن پیام" (Set) از Redis به SQL Server
                    await TransferSeenByStatusFromRedisToSqlAsync(context, redisDatabase);

                    _logger.LogInformation("Unread Message Sync Service work completed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in Unread Message Sync Service DoWork.");
                }
            }
        }

        /// <summary>
        /// همسان‌سازی وضعیت "دیده شدن پیام" از Redis Set (msg:seen:by:*) به جدول MessageReads در SQL.
        /// </summary>
        private async Task TransferSeenByStatusFromRedisToSqlAsync(IEMessengerDbContext context, IDatabase redisDatabase)
        {
            _logger.LogInformation("Attempting to transfer 'Seen By' status from Redis (pattern 'msg:seen:by:*') to SQL Server.");

            var server = redisDatabase.Multiplexer.GetServer(redisDatabase.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(database: redisDatabase.Database, pattern: "msg:seen:by:*");

            int transferredCount = 0;
            int emptyKeysDeleted = 0;

            var messageReadsToInsert = new List<MessageRead>();
            var usersToRemoveFromRedis = new Dictionary<RedisKey, List<RedisValue>>();

            foreach (var key in keys)
            {
                try
                {
                    // استخراج MessageId از کلید
                    var keyParts = key.ToString().Split(':');
                    if (keyParts.Length >= 3 && long.TryParse(keyParts[2], out long messageId))
                    {
                        // 1. دریافت تمام کاربران بیننده (اعضای Set)
                        var seenUsers = await redisDatabase.SetMembersAsync(key);

                        if (seenUsers.Length == 0)
                        {
                            await redisDatabase.KeyDeleteAsync(key);
                            emptyKeysDeleted++;
                            continue;
                        }

                        // 2. استخراج targetId و groupType از کلید
                        var parsedKeyInfo = ParseSeenByKey(key.ToString());
                        if (!parsedKeyInfo.IsValid)
                        {
                            _logger.LogWarning($"Invalid key format for 'msg:seen:by' key: {key}. Deleting.");
                            await redisDatabase.KeyDeleteAsync(key);
                            emptyKeysDeleted++;
                            continue;
                        }

                        // 3. بررسی و آماده‌سازی برای درج/حذف
                        foreach (var userIdValue in seenUsers)
                        {
                            if (userIdValue.TryParse(out long userId))
                            {
                                // بررسی وجود رکورد در دیتابیس
                                var existingReadStatus = await context.MessageReads
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(mr => mr.MessageId == messageId && mr.UserId == userId);

                                if (existingReadStatus == null)
                                {
                                    // اگر در SQL ثبت نشده بود، به لیست درج دسته‌ای اضافه کنید
                                    messageReadsToInsert.Add(new MessageRead
                                    {
                                        MessageId = messageId,
                                        UserId = userId,
                                        ReadDateTime = DateTime.UtcNow,
                                        GroupType = parsedKeyInfo.GroupType,
                                        TargetId = parsedKeyInfo.TargetId
                                    });
                                }

                                // کاربر را به لیست حذف از Redis اضافه کنید
                                if (!usersToRemoveFromRedis.ContainsKey(key))
                                {
                                    usersToRemoveFromRedis[key] = new List<RedisValue>();
                                }
                                usersToRemoveFromRedis[key].Add(userIdValue);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid key format for 'msg:seen:by' key: {key}. Deleting.");
                        await redisDatabase.KeyDeleteAsync(key);
                        emptyKeysDeleted++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing Redis key {key} during transfer to SQL. Skipping this key for retry.");
                }
            }

            // 4. درج دسته‌ای در SQL Server
            if (messageReadsToInsert.Any())
            {
                await context.MessageReads.AddRangeAsync(messageReadsToInsert);
                await context.SaveChangesAsync();
                transferredCount = messageReadsToInsert.Count;
                _logger.LogInformation($"Successfully inserted {transferredCount} new MessageRead entries into SQL Server.");
            }

            // 5. حذف دسته‌ای از Redis Sets
            var redisDeleteTasks = new List<Task>();
            foreach (var entry in usersToRemoveFromRedis)
            {
                var key = entry.Key;
                redisDeleteTasks.Add(redisDatabase.SetRemoveAsync(key, entry.Value.ToArray()));
            }
            await Task.WhenAll(redisDeleteTasks);
            _logger.LogInformation($"Successfully removed processed UserIds from Redis 'Seen By' Sets.");

            // 6. حذف کلیدهای Set خالی
            foreach (var key in usersToRemoveFromRedis.Keys)
            {
                var remainingCount = await redisDatabase.SetLengthAsync(key);
                if (remainingCount == 0)
                {
                    await redisDatabase.KeyDeleteAsync(key);
                    emptyKeysDeleted++;
                }
            }

            _logger.LogInformation($"Finished transferring. Transferred {transferredCount} read messages to SQL Server. Deleted {emptyKeysDeleted} empty/invalid Redis Set keys.");
        }

        // متد برای پارس کردن کلید و استخراج targetId و groupType
        private SeenByKeyInfo ParseSeenByKey(string redisKey)
        {
            try
            {
                // فرمت کلید: msg:seen:by:{messageId}:{targetId}:{groupType}
                var keyParts = redisKey.Split(':');

                if (keyParts.Length >= 6)
                {
                    return new SeenByKeyInfo
                    {
                        IsValid = true,
                        MessageId = long.Parse(keyParts[3]),
                        TargetId = int.Parse(keyParts[4]),
                        GroupType = keyParts[5]
                    };
                }
                // برای backward compatibility با کلیدهای قدیمی
                else if (keyParts.Length >= 4)
                {
                    return new SeenByKeyInfo
                    {
                        IsValid = false,
                        MessageId = 0,
                        TargetId = 0, // مقدار پیش‌فرض
                        GroupType = "" // مقدار پیش‌فرض
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing Redis key: {redisKey}");
            }

            return new SeenByKeyInfo { IsValid = false };
        }

        // کلاس کمکی برای نگهداری اطلاعات استخراج شده از کلید
        public class SeenByKeyInfo
        {
            public bool IsValid { get; set; }
            public long MessageId { get; set; }
            public int TargetId { get; set; }
            public string GroupType { get; set; }
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Unread Message Sync Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }


    }
}