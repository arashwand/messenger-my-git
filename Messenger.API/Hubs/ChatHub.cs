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
        private readonly ISystemMonitorService _systemMonitor;
        private readonly IUserService _userService;

        private const string BridgeGroupName = "BRIDGE_SERVICES";

        public ChatHub(IMessageService messageService,
                       IClassGroupService classGroupService,
                       IChannelService channelService,
                       ILogger<ChatHub> logger,
                       IRedisUserStatusService userStatusService,
                       RedisLastMessageService redisLastMessage,
                       IRedisUnreadManage redisUnreadManage,
                       PushService pushService,
                       IMessageQueueService messageQueueService,
                       ISystemMonitorService systemMonitor,
                       IUserService userService)
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
            _systemMonitor = systemMonitor;
            _userService = userService;
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

                // دریافت اطلاعات کاربر
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User {userId} not found in database");
                    Context.Abort();
                    return;
                }

                // 1. گروهها و کانالهای معمولی (کد موجود)
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

                // 2. ✅ گروههای چت خصوصی (Private 1-to-1)
                var privateChats = await _messageService.GetUserPrivateChatsAsync(userId);
                if (privateChats != null && privateChats.Any())
                {
                    foreach (var chat in privateChats.Where(c => !c.IsSystemChat && c.OtherUserId.HasValue))
                    {
                        var privateChatGroupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(userId, chat.OtherUserId.Value);
                        await Groups.AddToGroupAsync(Context.ConnectionId, privateChatGroupKey);
                        await _userStatusService.SetUserOnlineAsync(privateChatGroupKey, userId);
                        
                        _logger.LogInformation($"User {userId} joined private chat group: {privateChatGroupKey}");
                    }
                }

                // 3. ✅ گروه سیستمی شخصی
                var systemChatKey = PrivateChatHelper.GenerateSystemChatGroupKey(userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, systemChatKey);
                _logger.LogInformation($"User {userId} joined system chat group: {systemChatKey}");

                // 4. ✅ گروه نقش (برای پیامهای انبوه)
                var roleGroupKey = PrivateChatHelper.GenerateRoleGroupKey(user.RoleName);
                if (!string.IsNullOrEmpty(roleGroupKey))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, roleGroupKey);
                    _logger.LogInformation($"User {userId} joined role group: {roleGroupKey}");
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
            string badgeKey;

            if (groupType == ConstChat.PrivateType)
            {
                // برای چت خصوصی:  ساخت private_{minId}_{maxId}
                var minId = Math.Min(userId, targetId);
                var maxId = Math.Max(userId, targetId);
                badgeKey = $"private_{minId}_{maxId}";
                _logger.LogInformation($"✅ Generated Private badge key: {badgeKey} for userId={userId}, targetId={targetId}");
            }
            else
            {
                // برای گروه و کانال
                badgeKey = GenerateSignalRGroupKey.GenerateKey(targetId, groupType);
                _logger.LogInformation($"✅ Generated Group/Channel badge key: {badgeKey}");
            }

            _logger.LogInformation($"📤 Sending UpdateUnreadCount:  badgeKey={badgeKey}, unreadCount={unreadCount}, isBridge={isBridge}");

            await this.NotifyUserAndBridgeAsync(_logger,
                BridgeGroupName,
                userId,
                "UpdateUnreadCount",
                new object[] {
            badgeKey,  // پارامتر اول: key
            unreadCount  // پارامتر دوم: count
                },
                isBridgeSender: isBridge
            );

            _logger.LogInformation($"✅ UpdateUnreadCount sent successfully");
        }

        // =================== Helpers / Queries ===================
        public async Task<List<object>> GetUsersWithStatus(long groupId, string groupType)
        {
            var groupKey = (groupType == ConstChat.ClassGroupType ? ConstChat.ClassGroup : ConstChat.ChannelGroup) + groupId;
            var onlineUserIds = await _userStatusService.GetOnlineUsersAsync(groupKey);
            var onlineSet = new HashSet<long>(onlineUserIds);

            IEnumerable<UserDto> allUsers;
            if (groupType == ConstChat.ClassGroupType)
                allUsers = await _classGroupService.GetClassGroupMembersInternalAsync(groupId);
            else
                allUsers = await _channelService.GetChannelMembersInternalAsync(groupId);

            return allUsers.Select(u => new { UserId = u.UserId, UserName = u.NameFamily, ProfilePic = u.ProfilePicName, IsOnline = onlineSet.Contains(u.UserId) }).Cast<object>().ToList();
        }

        private (long groupId, string groupType) ParseGroupKey(string groupKey)
        {
            if (groupKey.StartsWith(ConstChat.ClassGroup))
            {
                long.TryParse(groupKey.Substring(ConstChat.ClassGroup.Length), out long id);
                return (id, ConstChat.ClassGroupType);
            }
            if (groupKey.StartsWith(ConstChat.ChannelGroup))
            {
                long.TryParse(groupKey.Substring(ConstChat.ChannelGroup.Length), out long id);
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
            long? groupId = null;
            if (request.GroupType != ConstChat.PrivateType && request.GroupType != "Private")
            {
                if (long.TryParse(request.GroupId, out var id))
                {
                    groupId = id;
                }
                else
                {
                    await Clients.Caller.SendAsync("SendMessageError", new { request.ClientMessageId, Error = "Invalid GroupId format." });
                    return;
                }
            }

            var savedMessageDto = await _messageService.SendGroupMessageAsync(request.UserId,
                request.GroupType == ConstChat.PrivateType ? request.GroupId : groupId.ToString(),
                request.GroupType, request.MessageText, request.FileAttachementIds, request.ReplyToMessageId);

            if (savedMessageDto == null)
            {
                await Clients.Caller.SendAsync("SendMessageError", request.ClientMessageId);
                return;
            }

            if (!string.IsNullOrEmpty(request.ClientMessageId)) savedMessageDto.ClientMessageId = request.ClientMessageId;

            // محاسبه groupKey و تنظیم ChatKey
            string groupKey;
            
            if (request.GroupType == ConstChat.PrivateType || request.GroupType == "Private")
            {
                // For private chats, the service layer resolves the GUID to the sender and receiver.
                // The DTO returns the other user's ID in the 'GroupId' field.
                long receiverId = savedMessageDto.GroupId;
                groupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(request.UserId, receiverId);
                
                // تنظیم ChatKey و GroupType
                savedMessageDto.ChatKey = groupKey;
                savedMessageDto.GroupType = "Private";
                
                _logger.LogInformation($"Private message: SenderId={request.UserId}, ReceiverId={receiverId}, ChatKey={groupKey}");
            }
            else
            {
                // برای Group/Channel
                groupKey = GenerateSignalRGroupKey.GenerateKey((long)groupId, request.GroupType);
                
                savedMessageDto.ChatKey = groupKey;
                savedMessageDto.GroupType = request.GroupType;
                savedMessageDto.GroupId = (long)groupId;
                
                _logger.LogInformation($"Group message: GroupId={groupId}, GroupType={request.GroupType}, ChatKey={groupKey}");
            }

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
            
            // ✅ برای Private: استفاده از groupKey (chatKey) به جای OwnerId
            if (request.GroupType == ConstChat.PrivateType || request.GroupType == "Private")
            {
                await _redisLastMessage.SetLastMessageAsync(request.GroupType, groupKey, chatMessageDto);
            }
            else
            {
                await _redisLastMessage.SetLastMessageAsync(request.GroupType, savedMessageDto.OwnerId.ToString(), chatMessageDto);
            }

            // ✅ برای Private: فقط یک گیرنده وجود دارد، برای Group/Channel: لیست اعضا را دریافت کن
            IEnumerable<UserDto> members;
            if (request.GroupType == ConstChat.PrivateType || request.GroupType == "Private")
            {
                // برای Private: فقط گیرنده را در نظر بگیر
                var receiverId = savedMessageDto.GroupId;
                members = new[] { new UserDto { UserId = receiverId } };
            }
            else if (request.GroupType == ConstChat.ClassGroupType)
            {
                members = await _classGroupService.GetClassGroupMembersInternalAsync(savedMessageDto.OwnerId);
            }
            else
            {
                members = await _channelService.GetChannelMembersInternalAsync(savedMessageDto.OwnerId);
            }

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
                // فاز 3: بررسی فشار سیستم (Load Balancing) - اولویت بالا
                // اگر سیستم تحت فشار است، همه پیامها به صف میروند
                var isSystemUnderPressure = await _systemMonitor.IsSystemUnderPressureAsync();
                if (isSystemUnderPressure)
                {
                    var loadScore = await _systemMonitor.GetSystemLoadScoreAsync();
                    _logger.LogWarning("System under pressure (Load Score: {LoadScore:F2}), queueing message with LOW priority", loadScore);
                    return (true, MessagePriority.Low);
                }

                if (request.GroupType != ConstChat.PrivateType)
                {
                    if (!long.TryParse(request.GroupId, out var groupId))
                    {
                        // Handle error for non-private chats with invalid group id
                        return (false, MessagePriority.Normal);
                    }

                    // فاز 1: بررسی تعداد اعضای گروه (Canary Deployment)
                    var memberCount = request.GroupType == ConstChat.ClassGroupType
                        ? await _classGroupService.GetClassGroupMembersCountAsync(groupId)
                        : await _channelService.GetChannelMembersCountAsync(groupId);

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
                }
                // فاز 4: آماده برای افزودن در آینده
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
                long? groupId = null;
                if (request.GroupType != ConstChat.PrivateType && request.GroupType != "Private")
                {
                    if (long.TryParse(request.GroupId, out var id))
                    {
                        groupId = id;
                    }
                    else
                    {
                        // Handle error, maybe send a message back to the caller
                        return;
                    }
                }

                var savedMessageDto = await _messageService.EditMessageAsync(request.MessageId, request.UserId,
                    request.GroupType == ConstChat.PrivateType ? request.GroupId : groupId.ToString(),
                    request.GroupType, request.MessageText, request.FileAttachementIds, request.FileIdsToRemove);

                if (savedMessageDto == null)
                {
                    await Clients.Caller.SendAsync("EditMessageSentFailed", request.UserId, request.MessageId);
                    return;
                }

                if (!string.IsNullOrEmpty(request.ClientMessageId)) savedMessageDto.ClientMessageId = request.ClientMessageId;

                // محاسبه groupKey و تنظیم ChatKey (مشابه SendMessage)
                string groupKey;
                
                if (request.GroupType == ConstChat.PrivateType || request.GroupType == "Private")
                {
                    long receiverId = savedMessageDto.GroupId;
                    groupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(request.UserId, receiverId);
                    
                    // تنظیم ChatKey و GroupType
                    savedMessageDto.ChatKey = groupKey;
                    savedMessageDto.GroupType = "Private";
                    
                    _logger.LogInformation($"Private message edited: SenderId={request.UserId}, ReceiverId={receiverId}, ChatKey={groupKey}");
                }
                else
                {
                    // برای Group/Channel
                    groupKey = GenerateSignalRGroupKey.GenerateKey((long)groupId, request.GroupType);
                    
                    savedMessageDto.ChatKey = groupKey;
                    savedMessageDto.GroupType = request.GroupType;
                    savedMessageDto.GroupId = (long)groupId;
                    
                    _logger.LogInformation($"Group message edited: GroupId={groupId}, GroupType={request.GroupType}, ChatKey={groupKey}");
                }

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

        public async Task Typing(long userId, string groupId, string groupType)
        {
            if (!IsBridge()) userId = GetCurrentUserId();
            var fullName = GetCurrentUserFullName();
            
            string groupKey;
            if (groupType == ConstChat.PrivateType)
            {
                var otherUserId = await _messageService.GetOtherUserIdInPrivateChat(groupId, userId);
                if(otherUserId == 0) return;
                groupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(userId, otherUserId);
            }
            else
            {
                groupKey = GenerateSignalRGroupKey.GenerateKey(long.Parse(groupId), groupType);
            }
            
            _logger.LogInformation("Typing event sent for user {UserId} in group {GroupKey}", userId, groupKey);
            await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                groupKey,
                "UserTyping",
                new object[] { userId, fullName, groupId },
                bridgeMethod: "UserTyping",
                bridgeArgs: new object[] { userId, fullName, groupId },
                isBridgeSender: IsBridge());
        }

        public async Task StopTyping(long userId, string groupId, string groupType)
        {
            if (!IsBridge()) userId = GetCurrentUserId();
            
            string groupKey;
            if (groupType == ConstChat.PrivateType)
            {
                var otherUserId = await _messageService.GetOtherUserIdInPrivateChat(groupId, userId);
                if(otherUserId == 0) return;
                groupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(userId, otherUserId);
            }
            else
            {
                groupKey = GenerateSignalRGroupKey.GenerateKey(long.Parse(groupId), groupType);
            }

            await this.BroadcastToGroupAndBridgeAsync(_logger, BridgeGroupName,
                groupKey,
                "UserStoppedTyping",
                new object[] { userId, groupId },
                bridgeMethod: "UserStoppedTyping",
                bridgeArgs: new object[] { userId, groupId },
                isBridgeSender: IsBridge());
        }

        public async Task MarkMessageAsRead(long currentUserId, string groupId, string groupType, long messageId)
        {
            if (currentUserId <= 0 || messageId <= 0) return;
            if (!IsBridge()) currentUserId = GetCurrentUserId();

            _logger.LogInformation($"MarkMessageAsRead called: userId={currentUserId}, groupId={groupId}, groupType={groupType}, messageId={messageId}");

            try
            {
                long targetId = 0;
                if (groupType == ConstChat.PrivateType)
                {
                    targetId = await _messageService.GetOtherUserIdInPrivateChat(groupId, currentUserId);
                    if (targetId == 0) return;
                }
                else
                {
                    if (!long.TryParse(groupId, out targetId)) return;
                }

                var senderUserId = await _messageService.MarkMessageAsReadAsync(messageId, currentUserId, targetId, groupType);
                if (senderUserId.HasValue && senderUserId.Value > 0)
                {
                    await _redisUnreadManage.MarkMessageAsSeenAsync(currentUserId, messageId, targetId, groupType);
                    var seenCount = await _redisUnreadManage.GetMessageSeenCountAsync(messageId);

                    if (senderUserId.Value != currentUserId)
                    {
                        await this.NotifyUserAndBridgeAsync(_logger, BridgeGroupName, senderUserId.Value, "MessageSeenUpdate", new object[] { messageId, currentUserId, seenCount, GetCurrentUserFullName() }, IsBridge());
                    }

                    await _redisUnreadManage.SetLastReadMessageIdAsync(currentUserId, targetId, groupType, messageId);
                    await _redisUnreadManage.DecrementUnreadCountAsync(currentUserId, targetId, groupType);
                    var unreadCount = await _redisUnreadManage.GetUnreadCountAsync(currentUserId, targetId, groupType);

                    _logger.LogInformation($"After mark as read: unreadCount={unreadCount}");

                    if (IsBridge())
                        await Clients.Caller.SendAsync("MessageSuccessfullyMarkedAsRead", messageId, groupId, groupType, unreadCount);
                    else
                        await Clients.Client(Context.ConnectionId).SendAsync("MessageSuccessfullyMarkedAsRead", messageId, groupId, groupType, unreadCount);

                    _logger.LogInformation($"🔔 Calling SendUnreadCountUpdateAsync for userId={currentUserId}, groupId={targetId}, groupType={groupType}, unreadCount={unreadCount}");
                    await SendUnreadCountUpdateAsync(currentUserId, targetId, groupType, unreadCount, IsBridge());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkMessageAsRead for message {MessageId} user {UserId}", messageId, currentUserId);
            }
        }

        public async Task MarkAllMessagesAsRead(long currentUserId, string groupId, string groupType)
        {
            if (!IsBridge()) currentUserId = GetCurrentUserId();

            long targetId = 0;
            if (groupType == ConstChat.PrivateType)
            {
                targetId = await _messageService.GetOtherUserIdInPrivateChat(groupId, currentUserId);
                if (targetId == 0) return;
            }
            else
            {
                if (!long.TryParse(groupId, out targetId)) return;
            }

            if (groupType == ConstChat.ClassGroupType && !await _classGroupService.IsUserMemberOfClassGroupAsync(currentUserId, targetId))
                throw new UnauthorizedAccessException("User is not a member of the group.");
            if (groupType == ConstChat.ChannelGroupType && !await _channelService.IsUserMemberOfChannelAsync(currentUserId, targetId))
                throw new UnauthorizedAccessException("User is not a member of the channel.");

            try
            {
                await _redisUnreadManage.ResetUnreadCountAsync(currentUserId, targetId, groupType);
                var allMessagesInChat = await _messageService.GetAllUnreadMessageInChat(currentUserId, targetId, groupType);

                if (allMessagesInChat == null || !allMessagesInChat.Any())
                {
                    if (!IsBridge())
                        await Clients.User(currentUserId.ToString()).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", new List<long>(), groupId, groupType, 0);
                    else
                        await Clients.Client(Context.ConnectionId).SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", new List<long>(), groupId, groupType, 0);
                    return;
                }

                long lastMessageIdInChat = allMessagesInChat.OrderByDescending(x => x.MessageId).First().MessageId;
                await _redisUnreadManage.SetLastReadMessageIdAsync(currentUserId, targetId, groupType, lastMessageIdInChat);

                var tasksMarkAsSeen = new List<Task>();
                foreach (var msg in allMessagesInChat) tasksMarkAsSeen.Add(_redisUnreadManage.MarkMessageAsSeenAsync(currentUserId, msg.MessageId, targetId, groupType));
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

                var finalUnreadCount = await _redisUnreadManage.GetUnreadCountAsync(currentUserId, targetId, groupType);
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
