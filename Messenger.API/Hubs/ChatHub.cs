using Messenger.API.ServiceHelper;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.DTOs;
using Messenger.Services;
using Messenger.Services.Classes;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Messenger.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IClassGroupService _classGroupService;
        private readonly IChannelService _channelService;
        private readonly IRedisUserStatusService _userStatusService;
        private readonly RedisLastMessageService _redisLastMessage;
        private readonly IRedisUnreadManage _redisUnreadManage;
        private readonly ILogger<ChatHub> _logger;
        private readonly PushService _pushService;
        private readonly IMessageQueueService _messageQueueService;

        private const string BridgeGroupName = "BRIDGE_SERVICES";

        public ChatHub(IMessageService messageService,
                       IClassGroupService classGroupService,
                       IChannelService channelService,
                       ILogger<ChatHub> logger,
                       IRedisUserStatusService userStatusService,
                       RedisLastMessageService redisLastMessage,
                       IRedisUnreadManage redisUnreadManage,
                       PushService pushService,
                       IMessageQueueService messageQueueService)
        {
            _messageService = messageService;
            _classGroupService = classGroupService;
            _channelService = channelService;
            _userStatusService = userStatusService;
            _redisLastMessage = redisLastMessage;
            _redisUnreadManage = redisUnreadManage;
            _logger = logger;
            _pushService = pushService;
            _messageQueueService = messageQueueService;
        }

        // =================== Connection lifecycle ===================
        public override async Task OnConnectedAsync()
        {
            try
            {
                // اگر کلاینت Bridge باشد، نیازی به منطق پیچیده نیست   
                var scopeClaim = Context.User.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
                if (scopeClaim?.Contains(ConstPolicies.BridgeService) == true)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, BridgeGroupName);
                    _logger.LogInformation($"Bridge service connected: {Context.ConnectionId}");
                    await base.OnConnectedAsync();
                    return;
                }

                var userId = GetCurrentUserId();
                if (userId <= 0)
                {
                    _logger.LogWarning("User with invalid token tried to connect. ConnectionId: {ConnectionId}", Context.ConnectionId);
                    Context.Abort();
                    return;
                }

                var groupKeys = await _userStatusService.GetUserGroupKeysAsync(userId);
                if (groupKeys == null || !groupKeys.Any())
                {
                    var groupKeysToCache = new List<string>();
                    var groups = await _classGroupService.GetUserClassGroupsAsync(userId);
                    if (groups != null)
                        groupKeysToCache.AddRange(groups.Select(g => ConstChat.ClassGroup + g.ClassId));

                    var channels = await _channelService.GetUserChannelsAsync(userId);
                    if (channels != null)
                        groupKeysToCache.AddRange(channels.Select(c => ConstChat.ChannelGroup + c.ChannelId));

                    await _userStatusService.CacheUserGroupKeysAsync(userId, groupKeysToCache);
                    groupKeys = groupKeysToCache.ToArray();
                }

                if (groupKeys != null && groupKeys.Any())
                {
                    foreach (var groupKey in groupKeys)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey);
                        await _userStatusService.SetUserOnlineAsync(groupKey, userId);

                        var (groupId, groupType) = ParseGroupKey(groupKey);
                        if (groupId > 0)
                        {
                            await Clients.Caller.SendAsync("UserStatusChanged", userId, true, groupId, groupType);
                        }
                    }
                }

                _ = PopulateUnreadCountsForUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for Connection {ConnectionId}", Context.ConnectionId);
                Context.Abort();
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var scopeClaim = Context.User?.FindFirst(c => c.Type == "scope")?.Value;
                if (scopeClaim?.Contains(ConstPolicies.BridgeService) == true)
                {
                    try
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BridgeGroupName);
                        _logger.LogInformation("Bridge connection {ConnectionId} removed from group {Group}", Context.ConnectionId, BridgeGroupName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove bridge connection from group {Group}", BridgeGroupName);
                    }

                    await base.OnDisconnectedAsync(exception);
                    return;
                }

                var userId = GetCurrentUserId();
                if (userId <= 0)
                {
                    await base.OnDisconnectedAsync(exception);
                    return;
                }

                var groupKeys = await _userStatusService.GetUserGroupKeysAsync(userId);
                if (groupKeys != null && groupKeys.Any())
                {
                    foreach (var groupKey in groupKeys)
                    {
                        await _userStatusService.SetUserOfflineAsync(groupKey, userId);
                        var (groupId, groupType) = ParseGroupKey(groupKey);
                        await Clients.Group(groupKey).SendAsync("UserStatusChanged", userId, false, groupId, groupType);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for Connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // =================== Presence / Heartbeat ===================
        public async Task SendHeartbeat(long userId)
        {
            if (!IsBridge())
            {
                userId = GetCurrentUserId();
            }

            if (userId <= 0) return;

            var groupKeys = await _userStatusService.GetUserGroupKeysAsync(userId);
            if (groupKeys != null && groupKeys.Any())
            {
                foreach (var groupKey in groupKeys)
                {
                    await _userStatusService.SetUserOnlineAsync(groupKey, userId);
                    var (groupId, groupType) = ParseGroupKey(groupKey);
                    await Clients.Caller.SendAsync("UserStatusChanged", userId, true, groupId, groupType);
                }
            }
            else
            {
                await AnnouncePresence(userId);
            }
        }

        [Authorize(Policy = "IsBridgeService")]
        public async Task AnnouncePresence(long userId)
        {
            if (userId <= 0) return;

            var groupKeys = await _userStatusService.GetUserGroupKeysAsync(userId);
            if (groupKeys == null || !groupKeys.Any())
            {
                var groupKeysToCache = new List<string>();
                var groups = await _classGroupService.GetUserClassGroupsAsync(userId);
                if (groups != null)
                    groupKeysToCache.AddRange(groups.Select(g => ConstChat.ClassGroup + g.ClassId));

                var channels = await _channelService.GetUserChannelsAsync(userId);
                if (channels != null)
                    groupKeysToCache.AddRange(channels.Select(c => ConstChat.ChannelGroup + c.ChannelId));

                await _userStatusService.CacheUserGroupKeysAsync(userId, groupKeysToCache);
                groupKeys = groupKeysToCache.ToArray();
            }

            if (groupKeys != null && groupKeys.Any())
            {
                foreach (var groupKey in groupKeys)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupKey);
                    await _userStatusService.SetUserOnlineAsync(groupKey, userId);
                    var (groupId, groupType) = ParseGroupKey(groupKey);
                    if (groupId > 0)
                        await Clients.Group(groupKey).SendAsync("UserStatusChanged", userId, true, groupId, groupType);
                }
            }
        }

        [Authorize(Policy = "IsBridgeService")]
        public async Task AnnounceDeparture(long userId)
        {
            if (userId <= 0) return;

            var groupKeys = await _userStatusService.GetUserGroupKeysAsync(userId);
            if (groupKeys == null || !groupKeys.Any()) return;

            foreach (var groupKey in groupKeys)
            {
                await _userStatusService.SetUserOfflineAsync(groupKey, userId);
                var (groupId, groupType) = ParseGroupKey(groupKey);
                await Clients.Caller.SendAsync("UserStatusChanged", userId, false, groupId, groupType);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupKey);
            }
        }

        // =================== Unread counts population ===================

        public async Task RequestUnreadCounts(long userId)
        {
            // بارگذاری اطلاعات خوانده نشده از دیتابیس به ردیس
            // جهت نمایش اینکه این کاربر در چت هایش چه تعداد پیام خوانده نشده دارد
            await PopulateUnreadCountsForUserAsync(userId);
        }

        private async Task PopulateUnreadCountsForUserAsync(long userId)
        {
            try
            {
                bool isBridge = IsBridge();
                var (groups, channels) = await GetUserGroupsAndChannelsAsync(userId);
                if ((groups == null || !groups.Any()) && (channels == null || !channels.Any())) return;

                var chatsToProcess = BuildChatsList(groups, channels);
                if (!chatsToProcess.Any()) return;

                foreach (var chat in chatsToProcess)
                {
                    await ProcessUnreadCountAsync(userId, chat.targetId, chat.groupType, isBridge);
                    await ProcessLastReadIdAsync(userId, chat.targetId, chat.groupType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating unread counts for user {UserId}", userId);
            }
        }

        private async Task<(IEnumerable<ClassGroupDto> groups, IEnumerable<ChannelDto> channels)> GetUserGroupsAndChannelsAsync(long userId)
        {
            var groups = await _classGroupService.GetUserClassGroupsAsync(userId);
            var channels = await _channelService.GetUserChannelsAsync(userId);
            return (groups, channels);
        }

        private List<(long targetId, string groupType)> BuildChatsList(IEnumerable<ClassGroupDto> groups, IEnumerable<ChannelDto> channels)
        {
            var list = new List<(long, string)>();
            if (groups != null) list.AddRange(groups.Select(g => (g.ClassId, ConstChat.ClassGroupType)));
            if (channels != null) list.AddRange(channels.Select(c => (c.ChannelId, ConstChat.ChannelGroupType)));
            return list;
        }

        private async Task ProcessUnreadCountAsync(long userId, long targetId, string groupType, bool isBridge)
        {
            try
            {
                var currentRedisCount = await _redisUnreadManage.GetUnreadCountAsync(userId, targetId, groupType);
                var finalUnreadCount = currentRedisCount;
                if (currentRedisCount == 0)
                {
                    int sqlUnreadCount = await _messageService.CalculateUnreadCountFromSqlAsync(userId, targetId, groupType);
                    if (sqlUnreadCount > 0)
                    {
                        await _redisUnreadManage.SetUnreadCountAsync(userId, targetId, groupType, sqlUnreadCount);
                        finalUnreadCount = sqlUnreadCount;
                    }
                }

                if (finalUnreadCount > 0)
                {
                    await SendUnreadCountUpdateAsync(userId, targetId, groupType, finalUnreadCount, isBridge);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing unread count for user {UserId} target {TargetId}", userId, targetId);
            }
        }

        private async Task ProcessLastReadIdAsync(long userId, long targetId, string groupType)
        {
            try
            {
                var currentLastReadId = await _redisUnreadManage.GetLastReadMessageIdAsync(userId, targetId, groupType);
                if (currentLastReadId == 0)
                {
                    long sqlLastReadId = await _messageService.GetLastReadMessageIdFromSqlAsync(userId, targetId, groupType);
                    if (sqlLastReadId > 0)
                    {
                        await _redisUnreadManage.SetLastReadMessageIdAsync(userId, targetId, groupType, sqlLastReadId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing last read id for user {UserId} target {TargetId}", userId, targetId);
            }
        }

        private async Task SendUnreadCountUpdateAsync(long userId, long targetId, string groupType, int unreadCount, bool isBridge)
        {
            await this.NotifyUserAndBridgeAsync(_logger,
                BridgeGroupName,
                userId,
                "UpdateUnreadCount",
                new object[] {
                    userId,
                    GenerateSignalRGroupKey.GenerateKey(targetId, groupType),
                    unreadCount
                },
                isBridgeSender: isBridge
            );
        }

        // =================== Helpers / Queries ===================
        public async Task<List<object>> GetUsersWithStatus(string groupId, string groupType)
        {
            var groupKey = (groupType == ConstChat.ClassGroupType ? ConstChat.ClassGroup : ConstChat.ChannelGroup) + groupId;
            var onlineUserIds = await _userStatusService.GetOnlineUsersAsync(groupKey);
            var onlineSet = new HashSet<long>(onlineUserIds);

            IEnumerable<UserDto> allUsers;
            if (groupType == ConstChat.ClassGroupType)
                allUsers = await _classGroupService.GetClassGroupMembersInternalAsync(int.Parse(groupId));
            else
                allUsers = await _channelService.GetChannelMembersInternalAsync(int.Parse(groupId));

            return allUsers.Select(u => new { UserId = u.UserId, UserName = u.NameFamily, ProfilePic = u.ProfilePicName, IsOnline = onlineSet.Contains(u.UserId) }).Cast<object>().ToList();
        }

        private (int groupId, string groupType) ParseGroupKey(string groupKey)
        {
            if (groupKey.StartsWith(ConstChat.ClassGroup))
            {
                int.TryParse(groupKey.Substring(ConstChat.ClassGroup.Length), out int id);
                return (id, ConstChat.ClassGroupType);
            }
            if (groupKey.StartsWith(ConstChat.ChannelGroup))
            {
                int.TryParse(groupKey.Substring(ConstChat.ChannelGroup.Length), out int id);
                return (id, ConstChat.ChannelGroupType);
            }
            return (0, string.Empty);
        }

        private bool IsBridge()
        {
            var scope = Context.User?.FindFirst(c => c.Type == "scope")?.Value;
            return scope?.Contains(ConstPolicies.BridgeService) == true;
        }

        // =================== Message operations ===================
        public async Task SendMessage(SendMessageRequestDto request)
        {
            bool isBridge = IsBridge();
            if (!isBridge) request.UserId = GetCurrentUserId();

            // اضافه کردن به صف Hangfire
            var queuedMessage = new QueuedMessageDto
            {
                UserId = request.UserId,
                GroupId = request.GroupId,
                GroupType = request.GroupType,
                MessageText = request.MessageText,
                FileAttachementIds = request.FileAttachementIds,
                ReplyToMessageId = request.ReplyToMessageId,
                ClientMessageId = request.ClientMessageId,
                QueuedAt = DateTime.UtcNow
            };

            // بررسی آیا پیام باید در صف قرار گیرد یا فوری ارسال شود
            var (shouldQueue, priority) = await DetermineIfShouldQueue(request);

            if (shouldQueue)
            {
                queuedMessage.Priority = priority;

                var jobId = _messageQueueService.EnqueueMessage(queuedMessage);

                _logger.LogInformation("Message queued with JobId: {JobId} from user {UserId} with priority {Priority}", 
                    jobId, request.UserId, priority);

                // اعلام به کاربر که پیام در صف قرار گرفت
                await Clients.Caller.SendAsync("MessageQueued", new
                {
                    jobId,
                    clientMessageId = request.ClientMessageId,
                    status = "queued",
                    priority = priority.ToString(),
                    estimatedProcessTime = "2-5 seconds"
                });

                return;
            }

            // ارسال فوری (کد قبلی)
            var savedMessageDto = await _messageService.SendGroupMessageAsync(request.UserId, request.GroupId, request.GroupType, request.MessageText, request.FileAttachementIds, request.ReplyToMessageId);
            if (savedMessageDto == null)
            {
                await Clients.Caller.SendAsync("SendMessageError", request.ClientMessageId);
                return;
            }

            if (!string.IsNullOrEmpty(request.ClientMessageId)) savedMessageDto.ClientMessageId = request.ClientMessageId;

            var groupKey = GenerateSignalRGroupKey.GenerateKey(request.GroupId, request.GroupType);

            await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                groupKey,
                "ReceiveMessage",
                new object[] { savedMessageDto },
                bridgeMethod: "ReceiveMessage",
                bridgeArgs: new object[] { savedMessageDto, request.GroupId, request.GroupType },
                isBridgeSender: isBridge);

            if (isBridge)
                await Clients.Caller.SendAsync("MessageSentSuccessfully", savedMessageDto);
            else
                await Clients.User(savedMessageDto.SenderUserId.ToString()).SendAsync("MessageSentSuccessfully", savedMessageDto);

            var chatMessageDto = new ChatMessageDto { MessageId = savedMessageDto.MessageId, SenderId = savedMessageDto.SenderUserId, SenderName = savedMessageDto.SenderUser?.NameFamily ?? string.Empty, SentAt = savedMessageDto.MessageDateTime, Text = savedMessageDto.MessageText?.MessageTxt };
            await _redisLastMessage.SetLastMessageAsync(request.GroupType, savedMessageDto.OwnerId.ToString(), chatMessageDto);

            var members = request.GroupType == ConstChat.ClassGroupType ?
                await _classGroupService.GetClassGroupMembersInternalAsync(savedMessageDto.OwnerId) :
                await _channelService.GetChannelMembersInternalAsync(savedMessageDto.OwnerId);

            var targetId = savedMessageDto.OwnerId;
            var tasks = new List<Task>();

            tasks.Add(_redisUnreadManage.SetLastReadMessageIdAsync(savedMessageDto.SenderUserId, targetId, request.GroupType, savedMessageDto.MessageId));

            // ایدی کاربران انلاین
            var onlineUsers = await _userStatusService.GetOnlineUsersAsync(groupKey);

            foreach (var member in members.Where(m => m.UserId != savedMessageDto.SenderUserId))
            {
                var memberId = member.UserId;
                tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(memberId, targetId, request.GroupType));
                tasks.Add(_redisUnreadManage.GetUnreadCountAsync(memberId, targetId, request.GroupType).ContinueWith(async t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Error retrieving unread count for member {MemberId} after increment.", memberId);
                        return;
                    }
                    var unreadCount = t.Result;
                    await this.NotifyUserAndBridgeAsync(_logger, BridgeGroupName, memberId, "UpdateUnreadCount", new object[] { memberId, groupKey, unreadCount }, isBridgeSender: isBridge);
                }).Unwrap());

                // اگر کاربر آنلاین نبود، پوش ارسال کن
                if (!onlineUsers.Contains(memberId))
                {
                    tasks.Add(_pushService.EnqueuePushAsync(memberId.ToString(), "پیام جدید",
                        $"{savedMessageDto.SenderUser?.NameFamily}: {savedMessageDto.MessageText?.MessageTxt}",
                        $"/")); //TODO ادرس چت اصلاح بشه. وقتی کاربر کلیک کرد چت مورد نظر باز بشه
                }
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// تصمیمگیری برای صفبندی یا ارسال فوری پیام با استراتژی فازبندی
        /// </summary>
        /// <returns>Tuple حاوی: (آیا باید در صف قرار گیرد، اولویت پیام)</returns>
        private async Task<(bool shouldQueue, MessagePriority priority)> DetermineIfShouldQueue(SendMessageRequestDto request)
        {
            try
            {
                // فاز 1: بررسی تعداد اعضای گروه (Canary Deployment)
                var memberCount = request.GroupType == ConstChat.ClassGroupType
                    ? await _classGroupService.GetClassGroupMembersCountAsync(request.GroupId)
                    : await _channelService.GetChannelMembersCountAsync(request.GroupId);

                if (memberCount > 50)
                {
                    // گروههای بزرگتر از 200 نفر با اولویت بالا
                    if (memberCount > 200)
                    {
                        _logger.LogInformation("Queueing message for large group (>{Count} members) with HIGH priority", memberCount);
                        return (true, MessagePriority.High);
                    }
                    
                    // گروههای 50-200 نفر با اولویت عادی
                    _logger.LogInformation("Queueing message for medium group ({Count} members) with NORMAL priority", memberCount);
                    return (true, MessagePriority.Normal);
                }

                // فاز 2: بررسی پیامهای حجیم (با 3 یا بیشتر فایل پیوست)
                if (request.FileAttachementIds != null && request.FileAttachementIds.Count >= 3)
                {
                    _logger.LogInformation("Queueing message with {FileCount} attachments with HIGH priority", 
                        request.FileAttachementIds.Count);
                    return (true, MessagePriority.High);
                }

                // فاز 3 و 4: آماده برای افزودن در آینده
                // - Load Balancing: بررسی فشار سیستم
                // - Scheduled Messages: پیامهای برنامهریزی شده

                // پیشفرض: ارسال فوری
                return (false, MessagePriority.Normal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DetermineIfShouldQueue, defaulting to immediate send");
                return (false, MessagePriority.Normal);
            }
        }

        public async Task EditMessage(EditMessageRequestDto request)
        {
            bool isBridge = IsBridge();
            if (!isBridge) request.UserId = GetCurrentUserId();

            try
            {
                var savedMessageDto = await _messageService.EditMessageAsync(request.MessageId, request.UserId, request.GroupId, request.GroupType, request.MessageText, request.FileAttachementIds, request.FileIdsToRemove);
                if (savedMessageDto == null)
                {
                    await Clients.Caller.SendAsync("EditMessageSentFailed", request.UserId, request.MessageId);
                    return;
                }

                if (!string.IsNullOrEmpty(request.ClientMessageId)) savedMessageDto.ClientMessageId = request.ClientMessageId;

                var groupKey = GenerateSignalRGroupKey.GenerateKey(request.GroupId, request.GroupType);

                await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                    groupKey,
                    "ReceiveEditedMessage",
                    new object[] { savedMessageDto },
                    bridgeMethod: "ReceiveEditedMessage",
                    bridgeArgs: new object[] { savedMessageDto, request.GroupId, request.GroupType },
                    isBridgeSender: isBridge);

                if (isBridge)
                    await Clients.Caller.SendAsync("EditMessageSentSuccessfully", savedMessageDto);
                else
                    await Clients.User(savedMessageDto.SenderUserId.ToString()).SendAsync("EditMessageSentSuccessfully", savedMessageDto);

            }
            catch (TimeLimitExceededException ex)
            {
                await Clients.Caller.SendAsync("EditMessageSentFailed", new
                {
                    userId = request.UserId,
                    messageId = request.MessageId,
                    errorCode = "TIME_LIMIT_EXCEEDED",
                    message = ex.Message,
                    allowedMinutes = ex.AllowedMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", request.MessageId);
                await Clients.Caller.SendAsync("EditMessageSentFailed", new
                {
                    userId = request.UserId,
                    messageId = request.MessageId,
                    errorCode = "SERVER_ERROR",
                    message = "خطای سرور در ویرایش پیام"
                });
            }
        }

        public async Task Typing(long userId, int groupId, string groupType)
        {
            if (!IsBridge()) userId = GetCurrentUserId();
            var fullName = GetCurrentUserFullName();
            var groupKey = GenerateSignalRGroupKey.GenerateKey(groupId, groupType);
            _logger.LogInformation("Typing event sent for user {UserId} in group {GroupKey}", userId, groupKey);
            await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                groupKey,
                "UserTyping",
                new object[] { userId, fullName, groupId },
                bridgeMethod: "UserTyping",
                bridgeArgs: new object[] { userId, fullName, groupId },
                isBridgeSender: IsBridge());
        }

        public async Task StopTyping(long userId, int groupId, string groupType)
        {
            if (!IsBridge()) userId = GetCurrentUserId();
            var groupKey = GenerateSignalRGroupKey.GenerateKey(groupId, groupType);

            await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                groupKey,
                "UserStoppedTyping",
                new object[] { userId, groupId },
                bridgeMethod: "UserStoppedTyping",
                bridgeArgs: new object[] { userId, groupId },
                isBridgeSender: IsBridge());
        }

        public async Task MarkMessageAsRead(long currentUserId, int groupId, string groupType, long messageId)
        {
            if (currentUserId <= 0 || messageId <= 0) return;
            if (!IsBridge()) currentUserId = GetCurrentUserId();

            try
            {
                var senderUserId = await _messageService.MarkMessageAsReadAsync(messageId, currentUserId, groupId, groupType);
                if (senderUserId.HasValue && senderUserId.Value > 0)
                {
                    await _redisUnreadManage.MarkMessageAsSeenAsync(currentUserId, messageId, groupId, groupType);
                    var seenCount = await _redisUnreadManage.GetMessageSeenCountAsync(messageId);

                    if (senderUserId.Value != currentUserId)
                    {
                        await this.NotifyUserAndBridgeAsync(_logger, BridgeGroupName, senderUserId.Value, "MessageSeenUpdate", new object[] { messageId, currentUserId, seenCount, GetCurrentUserFullName() }, IsBridge());
                    }

                    await _redisUnreadManage.SetLastReadMessageIdAsync(currentUserId, groupId, groupType, messageId);
                    await _redisUnreadManage.DecrementUnreadCountAsync(currentUserId, groupId, groupType);
                    var unreadCount = await _redisUnreadManage.GetUnreadCountAsync(currentUserId, groupId, groupType);

                    if (IsBridge())
                        await Clients.Caller.SendAsync("MessageSuccessfullyMarkedAsRead", messageId, groupId, groupType, unreadCount);
                    else
                        await Clients.Client(Context.ConnectionId).SendAsync("MessageSuccessfullyMarkedAsRead", messageId, groupId, groupType, unreadCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMessageAsRead for message {MessageId} user {UserId}", messageId, currentUserId);
            }
        }

        public async Task MarkAllMessagesAsRead(long currentUserId, int groupId, string groupType)
        {
            if (!IsBridge()) currentUserId = GetCurrentUserId();

            if (groupType == ConstChat.ClassGroupType && !await _classGroupService.IsUserMemberOfClassGroupAsync(currentUserId, groupId))
                throw new UnauthorizedAccessException("User is not a member of the group.");
            if (groupType == ConstChat.ChannelGroupType && !await _channelService.IsUserMemberOfChannelAsync(currentUserId, groupId))
                throw new UnauthorizedAccessException("User is not a member of the channel.");

            try
            {
                await _redisUnreadManage.ResetUnreadCountAsync(currentUserId, groupId, groupType);
                var allMessagesInChat = await _messageService.GetAllUnreadMessageInChat(currentUserId, groupId, groupType);

                if (allMessagesInChat == null || !allMessagesInChat.Any())
                {
                    if (!IsBridge())
                        await Clients.User(currentUserId.ToString()).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", new List<long>(), groupId, groupType, 0);
                    else
                        await Clients.Client(Context.ConnectionId).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", new List<long>(), groupId, groupType, 0);
                    return;
                }

                long lastMessageIdInChat = allMessagesInChat.OrderByDescending(x => x.MessageId).First().MessageId;
                await _redisUnreadManage.SetLastReadMessageIdAsync(currentUserId, groupId, groupType, lastMessageIdInChat);

                var tasksMarkAsSeen = new List<Task>();
                foreach (var msg in allMessagesInChat) tasksMarkAsSeen.Add(_redisUnreadManage.MarkMessageAsSeenAsync(currentUserId, msg.MessageId, groupId, groupType));
                await Task.WhenAll(tasksMarkAsSeen);

                var tasksNotify = new List<Task>();
                foreach (var msg in allMessagesInChat)
                {
                    if (msg.SenderUserId != currentUserId)
                    {
                        var currentSeenCount = await _redisUnreadManage.GetMessageSeenCountAsync(msg.MessageId);
                        tasksNotify.Add(this.NotifyUserAndBridgeAsync(_logger, BridgeGroupName, msg.SenderUserId, "MessageSeenUpdate", new object[] { msg.MessageId, currentUserId, currentSeenCount, GetCurrentUserFullName() }, IsBridge()));
                    }
                }
                await Task.WhenAll(tasksNotify);

                var finalUnreadCount = await _redisUnreadManage.GetUnreadCountAsync(currentUserId, groupId, groupType);
                var processedIds = allMessagesInChat.Select(m => m.MessageId).ToList();
                if (!IsBridge())
                    await Clients.User(currentUserId.ToString()).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", processedIds, groupId, groupType, finalUnreadCount);
                else
                    await Clients.Client(Context.ConnectionId).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", processedIds, groupId, groupType, finalUnreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAllMessagesAsRead for user {UserId} group {Group}", currentUserId, groupId);
            }
        }

        // =================== Small helpers ===================
        private long GetCurrentUserId() => long.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        private string GetCurrentUserRole() => Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        private string GetCurrentUserFullName() => Context.User?.FindFirst("NameFamily")?.Value ?? "--";
    }
}
