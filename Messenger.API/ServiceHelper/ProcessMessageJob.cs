using Hangfire;
using Hangfire.Server;
using Messenger.API.Hubs;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.DTOs;
using Messenger.Services;
using Messenger.Services.Classes;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.API.ServiceHelper
{
    /// <summary>
    /// Job پردازش و ارسال پیامهای صف شده
    /// </summary>
    public class ProcessMessageJob
    {
        private readonly IMessageService _messageService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IRedisUnreadManage _redisUnreadManage;
        private readonly RedisLastMessageService _redisLastMessage;
        private readonly IClassGroupService _classGroupService;
        private readonly IChannelService _channelService;
        private readonly PushService _pushService;
        private readonly IRedisUserStatusService _userStatusService;
        private readonly ILogger<ProcessMessageJob> _logger;

        public ProcessMessageJob(
            IMessageService messageService,
            IHubContext<ChatHub> hubContext,
            IRedisUnreadManage redisUnreadManage,
            RedisLastMessageService redisLastMessage,
            IClassGroupService classGroupService,
            IChannelService channelService,
            PushService pushService,
            IRedisUserStatusService userStatusService,
            ILogger<ProcessMessageJob> logger)
        {
            _messageService = messageService;
            _hubContext = hubContext;
            _redisUnreadManage = redisUnreadManage;
            _redisLastMessage = redisLastMessage;
            _classGroupService = classGroupService;
            _channelService = channelService;
            _pushService = pushService;
            _userStatusService = userStatusService;
            _logger = logger;
        }

        /// <summary>
        /// پردازش پیام از صف و ارسال آن
        /// این متد دقیقاً همان کارهایی را انجام میدهد که ChatHub.SendMessage انجام میدهد
        /// </summary>
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 60 })]
        public async Task ProcessAsync(QueuedMessageDto queuedMessage, PerformContext? context)
        {
            try
            {
                _logger.LogInformation("Processing queued message from user {UserId} to group {GroupId}",
                    queuedMessage.UserId, queuedMessage.GroupId);

                // 1. ذخیره پیام در دیتابیس
                var savedMessageDto = await _messageService.SendGroupMessageAsync(
                    queuedMessage.UserId,
                    queuedMessage.GroupId,
                    queuedMessage.GroupType,
                    queuedMessage.MessageText,
                    queuedMessage.FileAttachementIds,
                    queuedMessage.ReplyToMessageId);

                if (savedMessageDto == null)
                {
                    _logger.LogError("Failed to save message from user {UserId} to group {GroupId}",
                        queuedMessage.UserId, queuedMessage.GroupId);
                    throw new Exception("Failed to save message to database");
                }

                // تنظیم ClientMessageId در صورت وجود
                if (!string.IsNullOrEmpty(queuedMessage.ClientMessageId))
                {
                    savedMessageDto.ClientMessageId = queuedMessage.ClientMessageId;
                }

                _logger.LogInformation("Message saved with MessageId: {MessageId}", savedMessageDto.MessageId);

                // 2. ارسال پیام از طریق SignalR
                var groupKey = GenerateSignalRGroupKey.GenerateKey(queuedMessage.GroupId, queuedMessage.GroupType);

                await _hubContext.Clients.Group(groupKey).SendAsync("ReceiveMessage", savedMessageDto);
                _logger.LogInformation("Message broadcasted to SignalR group {GroupKey}", groupKey);

                // ارسال تایید به فرستنده
                await _hubContext.Clients.User(savedMessageDto.SenderUserId.ToString())
                    .SendAsync("MessageSentSuccessfully", savedMessageDto);

                // 3. بروزرسانی Redis Cache - آخرین پیام
                var chatMessageDto = new ChatMessageDto
                {
                    MessageId = savedMessageDto.MessageId,
                    SenderId = savedMessageDto.SenderUserId,
                    SenderName = savedMessageDto.SenderUser?.NameFamily ?? string.Empty,
                    SentAt = savedMessageDto.MessageDateTime,
                    Text = savedMessageDto.MessageText?.MessageTxt
                };

                await _redisLastMessage.SetLastMessageAsync(
                    queuedMessage.GroupType,
                    savedMessageDto.OwnerId.ToString(),
                    chatMessageDto);

                _logger.LogInformation("Redis last message updated for group {GroupId}", queuedMessage.GroupId);

                // 4. دریافت لیست اعضای گروه
                var members = queuedMessage.GroupType == ConstChat.ClassGroupType
                    ? await _classGroupService.GetClassGroupMembersInternalAsync(savedMessageDto.OwnerId)
                    : await _channelService.GetChannelMembersInternalAsync(savedMessageDto.OwnerId);

                var targetId = savedMessageDto.OwnerId;
                var tasks = new List<Task>();

                // تنظیم آخرین پیام خوانده شده برای فرستنده
                tasks.Add(_redisUnreadManage.SetLastReadMessageIdAsync(
                    savedMessageDto.SenderUserId,
                    targetId,
                    queuedMessage.GroupType,
                    savedMessageDto.MessageId));

                // دریافت لیست کاربران آنلاین
                var onlineUsers = await _userStatusService.GetOnlineUsersAsync(groupKey);

                // 5. بروزرسانی شمارنده پیامهای خوانده نشده و ارسال نوتیفیکیشن
                foreach (var member in members.Where(m => m.UserId != savedMessageDto.SenderUserId))
                {
                    var memberId = member.UserId;

                    // افزایش شمارنده unread
                    tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(memberId, targetId, queuedMessage.GroupType));

                    // ارسال آپدیت تعداد unread به کاربر
                    tasks.Add(UpdateUnreadCountForMemberAsync(memberId, targetId, queuedMessage.GroupType, groupKey));

                    // 6. ارسال Push notification برای کاربران آفلاین
                    if (!onlineUsers.Contains(memberId))
                    {
                        tasks.Add(_pushService.EnqueuePushAsync(
                            memberId.ToString(),
                            "پیام جدید",
                            $"{savedMessageDto.SenderUser?.NameFamily}: {savedMessageDto.MessageText?.MessageTxt}",
                            "/"));
                    }
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Successfully processed message {MessageId} from queue", savedMessageDto.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued message from user {UserId} to group {GroupId}",
                    queuedMessage.UserId, queuedMessage.GroupId);

                // در صورت خطا، Hangfire به صورت خودکار retry میکند
                throw;
            }
        }

        /// <summary>
        /// بروزرسانی شمارنده unread برای یک عضو و ارسال به کاربر
        /// </summary>
        private async Task UpdateUnreadCountForMemberAsync(long memberId, long targetId, string groupType, string groupKey)
        {
            try
            {
                var unreadCount = await _redisUnreadManage.GetUnreadCountAsync(memberId, (int)targetId, groupType);
                await _hubContext.Clients.User(memberId.ToString())
                    .SendAsync("UpdateUnreadCount", memberId, groupKey, unreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating unread count for member {MemberId}", memberId);
                // عدم ارسال exception تا Task.WhenAll به مشکل نخورد
            }
        }
    }
}
