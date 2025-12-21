using Messenger.DTOs;
using Messenger.API.ServiceHelper;
using Messenger.Services.Interfaces;
using Messenger.Models.Models;
using EFCore.BulkExtensions;

namespace Messenger.WebApp.ServiceHelper
{
    /// <summary>
    ///  هر  پنج دقیقه از دیتابیس (ردیس) میخونه و در دیتابیس(sql) ذخیره میکنه
    ///  این سرویس وظیفه دارد اخرین پیام 
    /// </summary>
    public class RedisToSqlSyncService : BackgroundService
    {
        // private readonly IEMessengerDbContext _dbContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RedisToSqlSyncService> _logger;

        public RedisToSqlSyncService(IServiceProvider serviceProvider,
            ILogger<RedisToSqlSyncService> logger)
        {
            //_dbContext = dbContext;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Redis to SQL Sync Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // هر 5 دقیقه یکبار اجرا می‌شود
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    _logger.LogInformation("Running scheduled sync from Redis to SQL.");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var cacheService = scope.ServiceProvider.GetRequiredService<IRedisCacheService>();

                        var keysToSync = await cacheService.GetAllLastReadKeysAsync();

                        if (!keysToSync.Any())
                        {
                            _logger.LogInformation("No keys to sync.");
                            continue;
                        }

                        var values = await cacheService.GetValuesForKeysAsync(keysToSync);

                        var updates = new List<UserClassGroup>();
                        foreach (var kvp in values)
                        {
                            // key format: "lastread:userId:groupId"
                            var parts = kvp.Key.Split(':');
                            if (parts.Length == 3 && long.TryParse(parts[1], out var userId) && int.TryParse(parts[2], out var groupId))
                            {
                                updates.Add(new UserClassGroup
                                {
                                    UserId = userId,
                                    ClassId = groupId,
                                    LastReadMessageId = long.Parse(kvp.Value)
                                });
                            }
                        }

                        if (updates.Any())
                        {
                            // استفاده از BulkExtensions برای یک آپدیت بهینه و دسته‌ای
                            var _dbContext = scope.ServiceProvider.GetRequiredService<IEMessengerDbContext>();

                            await _dbContext.BulkInsertOrUpdateAsync(updates);
                            _logger.LogInformation("Successfully synced {Count} records to SQL.", updates.Count);

                            // کلیدهای همگام‌سازی شده را از Redis پاک کن
                            await cacheService.DeleteKeysAsync(keysToSync);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in Redis to SQL Sync Service.");
                }
            }
        }
    }
}
