using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Messenger.Services.Interfaces.IRedisUnreadManage;

namespace Messenger.Services.Services
{
    public class RedisUnreadManage : IRedisUnreadManage
    {
        private readonly IDatabase _redis;
        private readonly ILogger<IRedisUnreadManage> _logger;

        // TTL برای شمارنده‌های کلی (Unread Count) و آخرین پیام خوانده شده (Last Read ID)
        // انتخاب 7 روز برای ماندگاری طولانی مدت این کلیدهای مهم.
        private readonly TimeSpan _unreadCountTTL = TimeSpan.FromDays(7);

        // TTL برای لیست کاربرانی که یک پیام را دیده‌اند (Seen By). 
        // می‌توان این مقدار را بر اساس نیاز به نگهداری جزئیات "Seen By" تنظیم کرد.
        private readonly TimeSpan _seenByTTL = TimeSpan.FromDays(30);

        public RedisUnreadManage(IConnectionMultiplexer redis, ILogger<RedisUnreadManage> logger)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        #region Unread Count Management

        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را برای یک کاربر در یک گروه/کانال افزایش می‌دهد.
        /// TTL برای کلید شمارنده اعمال یا تمدید می‌شود.
        /// </summary>
        public async Task IncrementUnreadCountAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var redisKey = GenerateRedisUnreadCountKey(userId, targetId, groupType);
                await _redis.StringIncrementAsync(redisKey);
                // اعمال یا تمدید TTL برای کلید شمارنده کلی
                await _redis.KeyExpireAsync(redisKey, _unreadCountTTL);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing unread count for user {UserId}, {GroupType} {TargetId}", userId, groupType, targetId);
                throw;
            }
        }


        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را کاهش می‌دهد (اگر بزرگ‌تر از صفر باشد).
        /// اگر شمارنده به صفر رسید، کلید را حذف می‌کند تا فضا آزاد شود.
        /// </summary>
        public async Task DecrementUnreadCountAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var redisKey = GenerateRedisUnreadCountKey(userId, targetId, groupType);
                var currentCount = await _redis.StringGetAsync(redisKey);

                if (currentCount.HasValue && currentCount.TryParse(out int currentCountValue))
                {
                    if (currentCountValue > 0)
                    {
                        await _redis.StringDecrementAsync(redisKey);
                        // تمدید TTL برای کلید شمارنده کلی
                        await _redis.KeyExpireAsync(redisKey, _unreadCountTTL);
                    }
                    else // اگر مقدار فعلی صفر یا کمتر بود، کلید را حذف کن
                    {
                        await _redis.KeyDeleteAsync(redisKey);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing unread count for user {UserId}, {GroupType} {TargetId}", userId, groupType, targetId);
                throw;
            }
        }


        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را برای یک گروه/کانال به صفر ریست می‌کند.
        /// با حذف کلید، فضای Redis آزاد می‌شود.
        /// </summary>
        public async Task ResetUnreadCountAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var redisKey = GenerateRedisUnreadCountKey(userId, targetId, groupType);
                await _redis.KeyDeleteAsync(redisKey); // کلید را حذف می‌کند
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting unread count for user {UserId}, {GroupType} {TargetId}", userId, groupType, targetId);
                throw;
            }
        }

        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را برای یک چت خاص دریافت می‌کند.
        /// </summary>
        public async Task<int> GetUnreadCountAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var redisKey = GenerateRedisUnreadCountKey(userId, targetId, groupType);
                var count = await _redis.StringGetAsync(redisKey);
                return (int)(count.HasValue ? count : 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread count for user {UserId}, {GroupType} {TargetId}", userId, groupType, targetId);
                throw;
            }
        }


        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را برای یک کاربر در تمام گروه‌ها و کانال‌های مشخص‌شده دریافت می‌کند.
        /// </summary>
        public async Task<Dictionary<string, int>> GetUnreadCountsAsync(long userId, IEnumerable<int> groupIds, IEnumerable<int> channelIds)
        {
            var unreadCounts = new Dictionary<string, int>();

            try
            {
                // Groups (از ConstChat.ClassGroupType استفاده می‌شود)
                foreach (var groupId in groupIds)
                {
                    var redisKey = GenerateRedisUnreadCountKey(userId, groupId, ConstChat.ClassGroupType);
                    var count = await _redis.StringGetAsync(redisKey);
                    unreadCounts[$"{ConstChat.ClassGroupType}:{groupId}"] = (int)(count.HasValue ? count : 0);
                }

                // Channels (از ConstChat.ChannelGroupType استفاده می‌شود)
                foreach (var channelId in channelIds)
                {
                    var redisKey = GenerateRedisUnreadCountKey(userId, channelId, ConstChat.ChannelGroupType);
                    var count = await _redis.StringGetAsync(redisKey);
                    unreadCounts[$"{ConstChat.ChannelGroupType}:{channelId}"] = (int)(count.HasValue ? count : 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread counts for user {UserId}", userId);
                throw;
            }

            return unreadCounts;
        }


        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده را برای یک کاربر در یک گروه یا کانال به یک مقدار خاص تنظیم می‌کند.
        /// </summary>
        public async Task SetUnreadCountAsync(long userId, long targetId, string groupType, int count)
        {
            try
            {
                var redisKey = GenerateRedisUnreadCountKey(userId, targetId, groupType);
                if (count <= 0)
                {
                    await _redis.KeyDeleteAsync(redisKey);
                }
                else
                {
                    await _redis.StringSetAsync(redisKey, count, _unreadCountTTL);
                }
                _logger.LogInformation("Set unread count for user {UserId}, {GroupType} {TargetId} to {Count}.", userId, groupType, targetId, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting unread count for user {UserId}, {GroupType} {TargetId} to {Count}.", userId, groupType, targetId, count);
                throw;
            }
        }


        #endregion

        #region Last Read Message ID Management

        /// <summary>
        /// آیدی آخرین پیام خوانده شده کاربر را در حافظه (Redis) برای یک چت خاص ست می‌کند.
        /// </summary>
        public async Task SetLastReadMessageIdAsync1(long userId, long targetId, string groupType, long messageId)
        {
            try
            {
                var redisKey = GenerateRedisLastReadKey(userId, targetId, groupType);

                // از TTL طولانی‌تر (_unreadCountTTL) استفاده می‌شود.
                await _redis.StringSetAsync(redisKey, messageId, _unreadCountTTL);

                _logger.LogInformation("Set last read message ID for user {UserId}, {GroupType} {TargetId} to {MessageId}.",
                                        userId, groupType, targetId, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting last read message ID for user {UserId}, {GroupType} {TargetId}, MessageId: {MessageId}",
                                  userId, groupType, targetId, messageId);
                throw;
            }
        }

        public async Task SetLastReadMessageIdAsync(long userId, long targetId, string groupType, long messageId)
        {
            // اگر آیدی پیام ورودی نامعتبر باشد، عملیاتی انجام نمی‌شود.
            if (messageId <= 0)
            {
                _logger.LogDebug("Invalid message ID {MessageId} for user {UserId}, {GroupType} {TargetId}. Operation skipped.",
                                messageId, userId, groupType, targetId);
                return;
            }

            try
            {
                // دریافت آیدی پیام آخرین پیام خوانده شده فعلی
                long currentLastReadId = await GetLastReadMessageIdAsync(userId, targetId, groupType);

                // فقط در صورتی به‌روزرسانی کنید که آیدی پیام جدید بزرگتر از آیدی فعلی باشد
                if (messageId > currentLastReadId)
                {
                    var redisKey = GenerateRedisLastReadKey(userId, targetId, groupType);

                    // ذخیره مقدار جدید با TTL طولانی‌تر
                    await _redis.StringSetAsync(redisKey, messageId, _unreadCountTTL);

                    _logger.LogInformation(
                        "Last read message ID updated for user {UserId}, {GroupType} {TargetId}: {PreviousId} → {MessageId}",
                        userId, groupType, targetId, currentLastReadId, messageId);
                }
                else
                {
                    _logger.LogDebug(
                        "Last read message ID update skipped for user {UserId} in {GroupType} {TargetId}. " +
                        "New ID {MessageId} ≤ current ID {CurrentId}",
                        userId, groupType, targetId, messageId, currentLastReadId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to set last read message ID for user {UserId}, {GroupType} {TargetId}, MessageId: {MessageId}",
                    userId, groupType, targetId, messageId);
                throw;
            }
        }

        /// <summary>
        /// آیدی آخرین پیام خوانده شده کاربر را برای یک چت خاص از حافظه (Redis) فراخوانی می‌کند.
        /// </summary>
        /// <returns>آیدی پیام (long). اگر کلید وجود نداشته باشد یا مقدار معتبر نباشد، 0 برگردانده می‌شود.</returns>
        public async Task<long> GetLastReadMessageIdAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var redisKey = GenerateRedisLastReadKey(userId, targetId, groupType);
                var lastReadId = await _redis.StringGetAsync(redisKey);

                if (lastReadId.HasValue && lastReadId.TryParse(out long messageId))
                {
                    return messageId;
                }

                // اگر کلید وجود نداشت یا مقدار آن معتبر نبود، 0 برمی‌گردانیم.
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last read message ID for user {UserId}, {GroupType} {TargetId}",
                                  userId, groupType, targetId);
                // در صورت خطا، 0 برمی‌گردانیم.
                return 0;
            }
        }

        #endregion

        #region Message Seen By Management (New Feature)

        /// <summary>
        /// یک پیام را به عنوان "دیده شده" توسط کاربر علامت‌گذاری می‌کند.
        /// آیدی کاربر را به یک Set مربوط به آن پیام در Redis اضافه می‌کند.
        /// </summary>
        public async Task MarkMessageAsSeenAsync(long userId, long messageId, long targetId, string groupType) // Note: types corrected for consistency
        {
            try
            {
                // کلید برای Set که فقط شامل کاربران بیننده است
                var redisKey = GenerateRedisSeenByKey(messageId);

                // از SetAddAsync استفاده می‌کنیم که معادل دستور SADD در Redis است
                // این دستور userId را به Set اضافه می‌کند. اگر از قبل وجود داشته باشد، هیچ اتفاقی نمی‌افتد.
                await _redis.SetAddAsync(redisKey, userId);

                // TTL را برای کلید تنظیم یا تمدید می‌کنیم
                await _redis.KeyExpireAsync(redisKey, _seenByTTL);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as seen by user {UserId}", messageId, userId);
                throw;
            }
        }

        // متد برای استخراج اطلاعات
        public async Task<MessageSeenDto> GetMessageSeenInfoAsync(long messageId)
        {
            try
            {
                var redisKey = GenerateRedisSeenByKey(messageId);
                var hashEntries = await _redis.HashGetAllAsync(redisKey);

                if (hashEntries.Length == 0)
                    return null;

                var seenInfo = new MessageSeenDto
                {
                    MessageId = messageId,
                    TargetId = (long)hashEntries.FirstOrDefault(x => x.Name == "target_id").Value,
                    GroupType = hashEntries.FirstOrDefault(x => x.Name == "group_type").Value,
                    SeenUserIds = hashEntries
                        .Where(x => x.Name == "seen_users")
                        .Select(x => long.Parse(x.Value))
                        .ToList()
                };

                return seenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting seen info for message {MessageId}", messageId);
                throw;
            }
        }

        /// <summary>
        /// تعداد کل کاربرانی که یک پیام خاص را دیده‌اند، دریافت می‌کند.
        /// </summary>
        /// <param name="messageId">آیدی پیام.</param>
        /// <returns>تعداد کاربران بیننده.</returns>
        public async Task<long> GetMessageSeenCountAsync(long messageId)
        {
            try
            {
                var redisKey = GenerateRedisSeenByKey(messageId);
                // SCARD: تعداد اعضای Set را برمی‌گرداند.
                return await _redis.SetLengthAsync(redisKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seen count for message {MessageId}", messageId);
                throw;
            }
        }

        /// <summary>
        /// لیست آیدی‌های کاربرانی که یک پیام خاص را دیده‌اند، دریافت می‌کند.
        /// </summary>
        /// <param name="messageId">آیدی پیام.</param>
        /// <returns>لیست آیدی‌های کاربران (long).</returns>
        public async Task<List<long>> GetUsersWhoSeenMessageAsync(long messageId)
        {
            try
            {
                var redisKey = GenerateRedisSeenByKey(messageId);
                // SMEMBERS: تمام اعضای Set را برمی‌گرداند.
                var members = await _redis.SetMembersAsync(redisKey);

                return members.Select(m => (long)m).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seen users for message {MessageId}", messageId);
                throw;
            }
        }



        #endregion

        #region for test in develop mode

        /// <summary>
        /// حذف همه اطلاعات و کلید ها
        /// </summary>
        /// <returns></returns>
        public async Task<int> ClearAllChatKeysAsync()
        {
            _logger.LogWarning("DANGER ZONE: Attempting to clear ALL chat-related Redis keys (chat:*, msg:seen:by:*).");

            var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
            var deletedCount = 0;

            // الگوی کلیدهای شمارنده و آخرین پیام خوانده شده: chat:*
            var chatKeys = server.Keys(database: _redis.Database, pattern: "chat:*").ToList();

            // الگوی کلیدهای Seen By: msg:seen:by:*
            var seenByKeys = server.Keys(database: _redis.Database, pattern: "msg:seen:by:*").ToList();

            var allKeys = chatKeys.Concat(seenByKeys).ToArray();

            if (allKeys.Any())
            {
                // اجرای حذف دسته‌ای
                deletedCount = (int)await _redis.KeyDeleteAsync(allKeys);
                _logger.LogWarning($"Successfully deleted {deletedCount} chat-related keys from Redis.");
            }
            else
            {
                _logger.LogInformation("No chat-related Redis keys found to delete.");
            }

            return deletedCount;
        }


        /// <summary>
        /// مشاهده تمام اطلاعات یک کاربر در حافظه redis
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>

        public async Task<Dictionary<string, string>> GetUserStatusReportAsync(long userId)
        {
            var report = new Dictionary<string, string>();
            var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());

            // الگوی کلیدهای Count و LastRead (فرمت جدید):
            // chat:unread:count:{targetId}:{groupType}:user:{userId}
            // chat:last:read:{targetId}:{groupType}:user:{userId}
            var chatKeysPattern = $"chat:*:user:{userId}";

            // الگوی کلیدهای Seen By Set (فرمت جدید):
            // msg:seen:by:{messageId}:{targetId}:{groupType}
            var seenByKeysPattern = "msg:seen:by:*";

            var allChatKeys = server.Keys(database: _redis.Database, pattern: chatKeysPattern).ToList();
            var allSeenByKeys = server.Keys(database: _redis.Database, pattern: seenByKeysPattern).ToList();

            _logger.LogInformation($"Generating Redis status report for User {userId}. " +
                                 $"Found {allChatKeys.Count} chat keys and {allSeenByKeys.Count} seen-by keys.");

            // ۱. پردازش کلیدهای Count و Last Read (Strings)
            if (allChatKeys.Any())
            {
                var keyValues = await _redis.StringGetAsync(allChatKeys.ToArray());

                for (int i = 0; i < allChatKeys.Count; i++)
                {
                    var key = allChatKeys[i].ToString();
                    var value = keyValues[i].ToString();

                    // تجزیه و تحلیل کلید برای نمایش خوانا
                    var parsedKeyInfo = ParseChatKey(key);
                    var displayKey = parsedKeyInfo.IsValid
                        ? $"{parsedKeyInfo.KeyType} (Target: {parsedKeyInfo.TargetId}, Type: {parsedKeyInfo.GroupType})"
                        : key;

                    report.Add(displayKey, value ?? "(NULL)");
                }
            }

            // ۲. پردازش کلیدهای Seen By (Sets)
            var userSeenMessages = new List<string>();

            foreach (var key in allSeenByKeys)
            {
                var keyString = key.ToString();

                // بررسی می‌کند که آیا کاربر عضو این Set است یا خیر
                var isMember = await _redis.SetContainsAsync(key, userId);
                if (isMember)
                {
                    // دریافت تعداد کل اعضا و اطلاعات کلید
                    var count = await _redis.SetLengthAsync(key);
                    var parsedKeyInfo = ParseSeenByKey(keyString);

                    if (parsedKeyInfo.IsValid)
                    {
                        var messageInfo = $"Message {parsedKeyInfo.MessageId} (Target: {parsedKeyInfo.TargetId}, Type: {parsedKeyInfo.GroupType}) - Readers: {count}";
                        userSeenMessages.Add(messageInfo);

                        // اضافه کردن به گزارش با جزئیات کامل
                        var reportKey = $"SEEN - Msg{parsedKeyInfo.MessageId}";
                        report.Add(reportKey, $"✅ Target: {parsedKeyInfo.TargetId}, Type: {parsedKeyInfo.GroupType}, Total Readers: {count}");
                    }
                    else
                    {
                        // برای کلیدهای قدیمی (backward compatibility)
                        report.Add(keyString, $"✅ SEEN - Total Readers: {count} (Legacy Key)");
                    }
                }
            }

            // ۳. جمع‌بندی و آمار
            var summary = new Dictionary<string, string>
            {
                ["Active Chats"] = allChatKeys.Count.ToString(),
                ["Total Seen Messages"] = userSeenMessages.Count.ToString(),
                ["Total Seen-By Keys Scanned"] = allSeenByKeys.Count.ToString()
            };

            // اضافه کردن آمار به گزارش
            foreach (var stat in summary)
            {
                report.Add($"📊 {stat.Key}", stat.Value);
            }

            // ۴. لیست پیام‌های دیده شده توسط کاربر
            if (userSeenMessages.Any())
            {
                report.Add("--- Seen Messages ---", "---");
                for (int i = 0; i < userSeenMessages.Count; i++)
                {
                    report.Add($"Seen #{i + 1}", userSeenMessages[i]);
                }
            }

            if (!report.Any())
            {
                report.Add("Status", $"No active chat-related Redis keys found for User ID {userId}.");
            }
            else
            {
                report.Add("Report Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            }

            return report;
        }

        // متد برای تجزیه کلیدهای Chat
        private ChatKeyInfo ParseChatKey(string redisKey)
        {
            try
            {
                // فرمت کلید: chat:{type}:{targetId}:{groupType}:user:{userId}
                // انواع: unread:count, last:read
                var keyParts = redisKey.Split(':');

                if (keyParts.Length >= 6)
                {
                    return new ChatKeyInfo
                    {
                        IsValid = true,
                        KeyType = $"{keyParts[1]}:{keyParts[2]}", // unread:count یا last:read
                        TargetId = long.Parse(keyParts[3]),
                        GroupType = keyParts[4],
                        UserId = long.Parse(keyParts[6])
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing chat key: {redisKey}");
            }

            return new ChatKeyInfo { IsValid = false };
        }

        // متد برای تجزیه کلیدهای Seen By (همان متد قبلی)
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
                        TargetId = long.Parse(keyParts[4]),
                        GroupType = keyParts[5]
                    };
                }
                // برای backward compatibility با کلیدهای قدیمی
                else if (keyParts.Length >= 4)
                {
                    return new SeenByKeyInfo
                    {
                        IsValid = false,
                        MessageId =0,
                        TargetId = 0,
                        GroupType = "Unknown"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing Redis key: {redisKey}");
            }

            return new SeenByKeyInfo { IsValid = false };
        }

        // کلاس کمکی برای اطلاعات کلیدهای Chat
        public class ChatKeyInfo
        {
            public bool IsValid { get; set; }
            public string KeyType { get; set; } // "unread:count" یا "last:read"
            public long TargetId { get; set; }
            public string GroupType { get; set; }
            public long UserId { get; set; }
        }

        // کلاس کمکی برای اطلاعات کلیدهای Seen By (همان قبلی)
        public class SeenByKeyInfo
        {
            public bool IsValid { get; set; }
            public long MessageId { get; set; }
            public long TargetId { get; set; }
            public string GroupType { get; set; }
        }

        #endregion

        #region Helper Methods (Key Generation)

        /// <summary>
        /// کلید Redis را برای شمارنده پیام‌های خوانده‌نشده (Unread Count) تولید می‌کند.
        /// فرمت: chat:unread:count:{groupType}:{targetId}:user:{userId}
        /// </summary>
        private string GenerateRedisUnreadCountKey(long userId, long targetId, string groupType)
        {
           
            var typePrefix = groupType == ConstChat.ClassGroupType ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
            return $"chat:unread:count:{typePrefix}:{targetId}:user:{userId}";
        }

        /// <summary>
        /// کلید Redis را برای آیدی آخرین پیام خوانده شده (Last Read Message ID) تولید می‌کند.
        /// فرمت: chat:last:read:{groupType}:{targetId}:user:{userId}
        /// </summary>
        private string GenerateRedisLastReadKey(long userId, long targetId, string groupType)
        {
            var typePrefix = groupType == ConstChat.ClassGroupType ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
            return $"chat:last:read:{typePrefix}:{targetId}:user:{userId}";
        }

        /// <summary>
        /// کلید Redis را برای مدیریت لیست کاربران بیننده (Seen By Set) تولید می‌کند.
        /// فرمت: msg:seen:by:{messageId}
        /// </summary>
        private string GenerateRedisSeenByKey(long messageId)
        {
            return $"msg:seen:by:{messageId}";
        }


        #endregion

    }
}