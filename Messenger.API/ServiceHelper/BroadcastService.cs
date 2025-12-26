using Messenger.API.Hubs;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Messenger.API.ServiceHelper
{
    public class BroadcastService : IBroadcastService
    {

        private readonly IHubContext<ChatHub> _hubContext;

        private readonly IEMessengerDbContext _context;
        private readonly IMessageService _messageService;
        private readonly ILogger<BroadcastService> _logger;
        private readonly IRedisUnreadManage _redisUnreadManage;
        private readonly IClassGroupService _classGroupService;
        private readonly IChannelService _channelService;
        private readonly RedisLastMessageService _redisLastMessage;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // گروهی که تمام نمونه‌های WebApp Bridge باید به آن بپیوندند
        private const string BridgeGroupName = "BRIDGE_SERVICES";

        public BroadcastService(IHubContext<ChatHub> hubContext, IMessageService messageService,
            IEMessengerDbContext context, ILogger<BroadcastService> logger,
            IRedisUnreadManage redisUnreadManage, IClassGroupService classGroupService,
            IChannelService channelService, RedisLastMessageService redisLastMessage, IHttpContextAccessor httpContextAccessor)
        {
            _messageService = messageService;
            _hubContext = hubContext;
            _context = context;
            _logger = logger;
            _redisUnreadManage = redisUnreadManage;
            _classGroupService = classGroupService;
            _channelService = channelService;
            _redisLastMessage = redisLastMessage;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// دریافت لیست آی‌دی کاربران بر اساس نقش آنها
        /// این متد لیستی از شناسه‌های کاربران را بر اساس نوع پیام (دانش‌آموزان، معلمان یا پرسنل) برمی‌گرداند
        /// از این متد برای ارسال پیام گروهی به دسته‌های مختلف کاربران استفاده می‌شود
        /// </summary>
        /// <param name="messageType">نوع پیام که مشخص می‌کند پیام برای کدام گروه از کاربران است</param>
        /// <returns>لیستی از شناسه‌های کاربران هدف</returns>
        /// <exception cref="ArgumentException">در صورتی که نوع پیام نامعتبر باشد این خطا رخ می‌دهد</exception>
        private async Task<List<long>> GetPeopleTargetIdsByRoleAsync(EnumMessageType messageType)
        {
            var roleName = messageType switch
            {
                EnumMessageType.AllStudents => ConstRoles.Student,
                EnumMessageType.AllTeachers => ConstRoles.Teacher,
                EnumMessageType.AllPersonel => ConstRoles.Personel,
                _ => throw new ArgumentException($"Unsupported message type for people targeting: {messageType}")
            };

            return await _context.Users
                .Where(w => w.RoleName == roleName)
                .Select(s => s.UserId)
                .ToListAsync();
        }

        /// <summary>
        /// ارسال به همه
        /// در اینجا تصمیم گیری میشه چطور و به کجا ارسال انجام بشه
        /// </summary>
        /// <param name="request">درخواست ارسال پیام به همه</param>
        /// <param name="senderUserId">ایدی ارسال کننده</param>
        /// <returns>نتیجه عملیات شامل تعداد دریافت‌کنندگان و پیام وضعیت</returns>
        /// <exception cref="ArgumentNullException">اگر درخواست خالی باشد</exception>
        /// <exception cref="ArgumentException">اگر نوع پیام نامعتبر باشد</exception>
        /// <exception cref="InvalidOperationException">اگر خطایی در ذخیره یا ارسال پیام رخ دهد</exception>
        public async Task<BroadcastResultDto> BroadcastAsync(long senderUserId, SendMessageToAllFromPortalDto request)
        {
            // گام 1: اعتبارسنجی ورودی
            // اگر درخواستی ارسال نشده یا متن پیام خالی باشد، با خطا خروجی می‌دهیم
            if (request == null)
            {
                _logger.LogError("BroadcastAsync received null request");
                throw new ArgumentNullException(nameof(request), "Broadcast request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.MessageText))
            {
                _logger.LogError("BroadcastAsync received empty message text");
                throw new ArgumentException("Message text cannot be empty", nameof(request));
            }

            try
            {
                // گام 2: ذخیره پیام در دیتابیس از طریق سرویس پیام‌ها
                // (SendMessageToAllAsync مسئول ساخت رکورد پیام کلی و برگرداندن DTO مربوطه است)
                var savedMessageDto = await _messageService.SendMessageToAllAsync(
                    userId: senderUserId,
                    messageType: request.MessageType,
                    messageText: request.MessageText,
                    isPin: request.IsPin,
                    isPortalMessage: true);

                // اگر ذخیره موفق نباشد؛ عملیات متوقف و خطا پرتاب می‌شود
                if (savedMessageDto == null)
                {
                    _logger.LogError("Failed to save message in SendMessageToAllAsync");
                    throw new InvalidOperationException("Failed to save broadcast message");
                }

                int targetIdsCount = 0;

                // گام 3: بررسی نوع پیام و شاخه‌بندی بر اساس اینکه پیام برای گروه‌ها/کانال‌هاست یا برای کاربران با نقش خاص
                if (request.MessageType == EnumMessageType.AllGroups || request.MessageType == EnumMessageType.AllChannels)
                {
                    // شاخه مربوط به ارسال به همه گروه‌ها یا همه کانال‌ها
                    try
                    {
                        List<long> targetIds;
                        List<string> chatTargetKeys;

                        if (request.MessageType == EnumMessageType.AllGroups)
                        {
                            // 3.a - گرفتن لیست آیدی کلاس‌ها از دیتابیس
                            targetIds = await _context.ClassGroups.Select(c => c.ClassId).ToListAsync();
                            // تولید کلیدهای گروه سیگنال‌آر برای ارسال به گروه‌ها
                            chatTargetKeys = targetIds.Select(id => GenerateSignalRGroupKey.GenerateKey(id, ConstChat.ClassGroupType)).ToList();
                        }
                        else
                        {
                            // 3.b - گرفتن لیست آیدی کانال‌ها از دیتابیس
                            targetIds = await _context.Channels.Select(c => c.ChannelId).ToListAsync();
                            chatTargetKeys = targetIds.Select(id => GenerateSignalRGroupKey.GenerateKey(id, ConstChat.ChannelGroupType)).ToList();
                        }

                        // اگر هیچ گروهی وجود نداشت، با پیام مناسب بازگردانده می‌شود
                        targetIdsCount = chatTargetKeys.Count;
                        if (targetIdsCount == 0)
                        {
                            _logger.LogWarning("No recipients found for message type {MessageType}", request.MessageType);
                            return new BroadcastResultDto
                            {
                                MessageText = $"No recipients found for {request.MessageType}",
                                TargetIdsCount = 0
                            };
                        }

                        // گام 4: ارسال پیام به تمام گروه‌های هدف از طریق SignalR در یک فراخوانی گروهی
                        await _hubContext.BroadcastToGroupsAndBridgeAsync(_logger, BridgeGroupName, chatTargetKeys, "BroadcastToGroups",
                            new object[] { savedMessageDto, request.MessageType, targetIds });

                        _logger.LogInformation("Successfully sent broadcast message to {Count} {Type}",
                            targetIdsCount,
                            request.MessageType == EnumMessageType.AllGroups ? "groups" : "channels");

                        // گام 5: بروزرسانی حالت داخلی (Redis) برای هر گروه:
                        // - بروزرسانی آخرین پیام
                        // - افزایش شمارنده unread برای هر عضو
                        var groupType = request.MessageType == EnumMessageType.AllGroups ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
                        var tasks = new List<Task>();

                        foreach (var tid in targetIds)
                        {
                            // تهیه DTO ساده‌شده برای آخرین پیام چت
                            var chatMessageDto = new ChatMessageDto
                            {
                                MessageId = savedMessageDto.MessageId,
                                SenderId = savedMessageDto.SenderUserId,
                                SenderName = savedMessageDto.SenderUser?.NameFamily ?? string.Empty,
                                SentAt = savedMessageDto.MessageDateTime,
                                Text = savedMessageDto.MessageText?.MessageTxt
                            };

                            // 5.a - ذخیره آخرین پیام در Redis برای هر چت
                            tasks.Add(_redisLastMessage.SetLastMessageAsync(groupType, tid.ToString(), chatMessageDto));

                            // 5.b - دریافت اعضای هر گروه/کانال تا بتوانیم unread را برای هر عضو افزایش دهیم
                            var members = groupType == ConstChat.ClassGroupType
                                ? await _classGroupService.GetClassGroupMembersInternalAsync(tid)
                                : await _channelService.GetChannelMembersInternalAsync(tid);

                            if (members != null && members.Any())
                            {
                                var groupKey = GenerateSignalRGroupKey.GenerateKey(tid, groupType);
                                foreach (var member in members.Where(m => m.UserId != savedMessageDto.SenderUserId))
                                {
                                    var memberId = member.UserId;

                                    // 5.c - افزایش شمارشگر unread برای هر عضو
                                    tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(memberId, tid, groupType));

                                    // 5.d - پس از افزایش، مقدار جدید unread را خوانده و از طریق سیگنال‌آر به آن کاربر اطلاع می‌دهیم
                                    tasks.Add(_redisUnreadManage.GetUnreadCountAsync(memberId, tid, groupType).ContinueWith(async tResult =>
                                    {
                                        if (tResult.IsFaulted)
                                        {
                                            _logger.LogError(tResult.Exception, "Error retrieving unread count for member {MemberId} after increment.", memberId);
                                            return;
                                        }

                                        var unreadCount = tResult.Result;
                                        await _hubContext.Clients.User(memberId.ToString()).SendAsync("UpdateUnreadCount", memberId, groupKey, unreadCount);
                                        await _hubContext.Clients.Group(BridgeGroupName).SendAsync("UpdateUnreadCount", memberId, groupKey, unreadCount);
                                    }).Unwrap());
                                }
                            }
                        }

                        // گام 6: منتظر ماندن برای اتمام تمام عملیات ناهمزمان
                        await Task.WhenAll(tasks);
                    }
                    catch (Exception ex)
                    {
                        // لاگ و تبدیل به InvalidOperationException برای مشخص کردن شکست عملیات broadcast
                        _logger.LogError(ex, "Error broadcasting to {MessageType}: {Error}", request.MessageType, ex.Message);
                        throw new InvalidOperationException($"Failed to broadcast message to {request.MessageType}", ex);
                    }
                }
                else if (request.MessageType is EnumMessageType.AllStudents
                         or EnumMessageType.AllTeachers
                         or EnumMessageType.AllPersonel)
                {
                    // شاخه مربوط به ارسال به کاربران بر اساس نقش
                    try
                    {
                        // 4.a - گرفتن لیست کاربران هدف براساس نقش
                        var peopleTargetIds = await GetPeopleTargetIdsByRoleAsync(request.MessageType);
                        targetIdsCount = peopleTargetIds.Count;

                        if (targetIdsCount == 0)
                        {
                            _logger.LogWarning("No recipients found for message type {MessageType}", request.MessageType);
                            return new BroadcastResultDto
                            {
                                MessageText = $"No recipients found for {request.MessageType}",
                                TargetIdsCount = 0
                            };
                        }

                        // 4.b - ارسال پیام به شناسه‌های کاربران مشخص شده از طریق SignalR Users
                        var userIdentifiers = peopleTargetIds.Select(id => id.ToString());

                        await _hubContext.BroadcastToUsersAndBridgeAsync(_logger, BridgeGroupName, userIdentifiers, "BroadcastToUsers",
                            new object[] { savedMessageDto, request.MessageType, peopleTargetIds });

                        _logger.LogInformation("Successfully sent broadcast message to {Count} recipients of type {MessageType}",
                            targetIdsCount,
                            request.MessageType);

                        // 4.c - افزایش شمارنده unread برای هر کاربر (در این حالت targetId و groupType خاصی نداریم)
                        var tasks = new List<Task>();
                        foreach (var userId in peopleTargetIds)
                        {
                            tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(userId, 0, string.Empty));
                            // ارسال UpdateUnreadCount
                            tasks.Add(_redisUnreadManage.GetUnreadCountAsync(userId, 0, string.Empty).ContinueWith(async tResult =>
                            {
                                if (tResult.IsFaulted)
                                {
                                    _logger.LogError(tResult.Exception, "Error retrieving unread count for user {UserId}", userId);
                                    return;
                                }

                                var unreadCount = tResult.Result;
                                await _hubContext.Clients.User(userId.ToString()).SendAsync("UpdateUnreadCount", userId, "", unreadCount);
                                await _hubContext.Clients.Group(BridgeGroupName).SendAsync("UpdateUnreadCount", userId, "", unreadCount);
                            }).Unwrap());
                        }
                        await Task.WhenAll(tasks);
                    }
                    catch (ArgumentException ex)
                    {
                        // خطاهای مرتبط با نوع پیام را لاگ و دوباره پراکنده می‌کنیم
                        _logger.LogError(ex, "Invalid message type while sending message: {MessageType}", request.MessageType);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // سایر خطاها را لاگ و به صورت InvalidOperationException بالا می‌بریم
                        _logger.LogError(ex, "Error broadcasting to {MessageType}: {Error}", request.MessageType, ex.Message);
                        throw new InvalidOperationException($"Failed to broadcast message to {request.MessageType}", ex);
                    }
                }
                else
                {
                    // اگر نوع پیام پشتیبانی نشد، خطای ورودی می‌دهیم
                    _logger.LogError("Unsupported MessageType while sending message: {MessageType}", request.MessageType);
                    throw new ArgumentException($"Unsupported message type: {request.MessageType}");
                }

                // گام نهایی: بازگشت نتیجه موفق شامل تعداد گیرندگان
                return new BroadcastResultDto
                {
                    MessageText = $"Successfully sent message to {targetIdsCount} recipients",
                    TargetIdsCount = targetIdsCount
                };
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                // لاگ برای خطاهای غیرمنتظره و تبدیل به InvalidOperationException عمومی
                _logger.LogError(ex, "Unexpected error in BroadcastAsync: {Error}", ex.Message);
                throw new InvalidOperationException("An unexpected error occurred while broadcasting message", ex);
            }
        }

        /// <summary>
        /// ارسال پیام به یک هدف خاص: فرد، گروه یا کانال
        /// این متد پیام را ذخیره کرده و از طریق SignalR ارسال می‌کند
        /// </summary>
        /// <param name="senderUserId">ایدی ارسال کننده پیام</param>
        /// <param name="request">درخواست ارسال پیام شامل نوع هدف، ایدی هدف و متن پیام</param>
        /// <returns>نتیجه عملیات ارسال پیام</returns>
        /// <exception cref="ArgumentNullException">اگر درخواست خالی باشد</exception>
        /// <exception cref="ArgumentException">اگر نوع هدف نامعتبر باشد یا متن پیام خالی باشد</exception>
        /// <exception cref="InvalidOperationException">اگر خطایی در ذخیره یا ارسال پیام رخ دهد</exception>
        public async Task<BroadcastResultDto> SendMessageAsync(long senderUserId, SendMessageToTargetDto request)
        {
            // گام ۱: اعتبارسنجی ورودی
            if (request == null)
            {
                _logger.LogError("SendMessageAsync received null request");
                throw new ArgumentNullException(nameof(request), "Request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.MessageText))
            {
                _logger.LogError("SendMessageAsync received empty message text");
                throw new ArgumentException("Message text cannot be empty", nameof(request.MessageText));
            }

            if (string.IsNullOrWhiteSpace(request.TargetType) || request.TargetId <= 0)
            {
                _logger.LogError("SendMessageAsync received invalid target type or id");
                throw new ArgumentException("Invalid target type or id", nameof(request));
            }

            try
            {
                MessageDto savedMessageDto = null;

                // گام ۲: ذخیره پیام در دیتابیس بر اساس نوع هدف
                if (request.TargetType == ConstChat.PrivateType)
                {
                    // ارسال پیام خصوصی
                    savedMessageDto = await _messageService.SendPrivateMessageAsync(
                        senderUserId: senderUserId,
                        receiverUserId: request.TargetId,
                        messageText: request.MessageText,
                        files: request.FileIds,
                        isPortalMessage: true
                    );
                }
                else if (request.TargetType == ConstChat.ClassGroupType || request.TargetType == ConstChat.ChannelGroupType)
                {
                    // ارسال پیام به گروه یا کانال
                    var groupType = request.TargetType == ConstChat.ClassGroupType ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
                    savedMessageDto = await _messageService.SendGroupMessageAsync(
                        senderUserId: senderUserId,
                        chatId: request.TargetId.ToString(),
                        groupType: groupType,
                        messageText: request.MessageText,
                        files: request.FileIds,
                        isPin: request.IsPin,
                        isPortalMessage: true
                    );
                }
                else
                {
                    _logger.LogError("Unsupported TargetType: {TargetType}", request.TargetType);
                    throw new ArgumentException($"Unsupported target type: {request.TargetType}");
                }

                if (savedMessageDto == null)
                {
                    _logger.LogError("Failed to save message in SendMessageAsync");
                    throw new InvalidOperationException("Failed to save message");
                }

                // تنظیم GroupType و GroupId قبل از ارسال
                savedMessageDto.GroupType = request.TargetType;
                
                if (request.TargetType == ConstChat.PrivateType)
                {
                    // برای Private: groupId در Bridge محاسبه میشود - اینجا فقط metadata را تنظیم میکنیم
                    savedMessageDto.OwnerId = request.TargetId; // receiverId
                }
                else
                {
                    // برای Group/Channel: groupId همان targetId است
                    savedMessageDto.GroupId = request.TargetId;
                }

                // گام ۳: ارسال پیام از طریق SignalR با استفاده از HubExtensions
                await _hubContext.SendMessageToTargetAndBridgeAsync(_logger, BridgeGroupName, request.TargetType, request.TargetId, savedMessageDto);

                // گام ۴: بروزرسانی Redis برای آخرین پیام و unread counts
                var tasks = new List<Task>();

                if (request.TargetType == ConstChat.PrivateType)
                {
                    // برای پیام خصوصی، افزایش unread برای گیرنده
                    tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(request.TargetId, 0, string.Empty));
                    tasks.Add(_redisUnreadManage.GetUnreadCountAsync(request.TargetId, 0, string.Empty).ContinueWith(async tResult =>
                    {
                        if (tResult.IsFaulted)
                        {
                            _logger.LogError(tResult.Exception, "Error retrieving unread count for user {UserId}", request.TargetId);
                            return;
                        }
                        var unreadCount = tResult.Result;
                        await _hubContext.Clients.User(request.TargetId.ToString()).SendAsync("UpdateUnreadCount", request.TargetId, "", unreadCount);
                        await _hubContext.Clients.Group(BridgeGroupName).SendAsync("UpdateUnreadCount", request.TargetId, "", unreadCount);
                    }).Unwrap());
                }
                else
                {
                    // برای گروه یا کانال
                    var groupType = request.TargetType == ConstChat.ClassGroupType ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
                    var chatMessageDto = new ChatMessageDto
                    {
                        MessageId = savedMessageDto.MessageId,
                        SenderId = savedMessageDto.SenderUserId,
                        SenderName = savedMessageDto.SenderUser?.NameFamily ?? string.Empty,
                        SentAt = savedMessageDto.MessageDateTime,
                        Text = savedMessageDto.MessageText?.MessageTxt

                    };

                    // ذخیره آخرین پیام
                    tasks.Add(_redisLastMessage.SetLastMessageAsync(groupType, request.TargetId.ToString(), chatMessageDto));

                    // دریافت اعضای گروه/کانال
                    var members = groupType == ConstChat.ClassGroupType
                        ? await _classGroupService.GetClassGroupMembersInternalAsync(request.TargetId)
                        : await _channelService.GetChannelMembersInternalAsync(request.TargetId);

                    if (members != null && members.Any())
                    {
                        var groupKey = GenerateSignalRGroupKey.GenerateKey(request.TargetId, groupType);
                        foreach (var member in members.Where(m => m.UserId != savedMessageDto.SenderUserId))
                        {
                            var memberId = member.UserId;
                            tasks.Add(_redisUnreadManage.IncrementUnreadCountAsync(memberId, request.TargetId, groupType));
                            tasks.Add(_redisUnreadManage.GetUnreadCountAsync(memberId, request.TargetId, groupType).ContinueWith(async tResult =>
                            {
                                if (tResult.IsFaulted)
                                {
                                    _logger.LogError(tResult.Exception, "Error retrieving unread count for member {MemberId}", memberId);
                                    return;
                                }
                                var unreadCount = tResult.Result;
                                await _hubContext.Clients.User(memberId.ToString()).SendAsync("UpdateUnreadCount", memberId, groupKey, unreadCount);
                                await _hubContext.Clients.Group(BridgeGroupName).SendAsync("UpdateUnreadCount", memberId, groupKey, unreadCount);
                            }).Unwrap());
                        }
                    }
                }

                // منتظر اتمام عملیات Redis
                await Task.WhenAll(tasks);

                // گام نهایی: بازگشت نتیجه
                return new BroadcastResultDto
                {
                    MessageText = $"Successfully sent message to {request.TargetType} {request.TargetId}",
                    TargetIdsCount = 1 // برای یک هدف
                };
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Unexpected error in SendMessageAsync: {Error}", ex.Message);
                throw new InvalidOperationException("An unexpected error occurred while sending message", ex);
            }
        }


        /// <summary>
        /// اطلاع رسانی به گروه یا کانال درباره تغییر وضعیت پین یک پیام
        /// </summary>
        /// <param name="messageEntity"></param>
        /// <returns></returns>
        public async Task NotifyGroupAboutPinAsync(long userId, long messageId, bool isPinned)
        {
            try
            {
                // ابتدا وضعیت پین را در لایه سرویس بروزرسانی کن
                await _messageService.PinMessageAsync(userId, messageId, isPinned);

                // سپس DTO بروزشده پیام را گرفته و از طریق HubExtensions اطلاع‌رسانی کن
                var updatedMessageDto = await _messageService.GetMessageByIdAsync(userId, messageId);
                if (updatedMessageDto == null)
                {
                    _logger.LogWarning("Pinned message DTO for ID {MessageId} could not be loaded", messageId);
                    return;
                }

                //TODO: برای چت دونفره هم باید تعین شود که پیام پین شده است یا نه
                // تعیین نوع و شناسه هدف برای اطلاع‌رسانی
                string targetType = "";

                targetType = updatedMessageDto.MessageType == (byte)EnumMessageType.Private ? ConstChat.PrivateType : "";

                // اگر نوع هدف هنوز تعیین نشده، یا گروه است یا کانال
                if (targetType == "")
                {
                    targetType = updatedMessageDto.MessageType == (byte)EnumMessageType.Group ? ConstChat.ClassGroupType : ConstChat.ChannelGroupType;
                }

                long targetId = updatedMessageDto.OwnerId;

                // استفاده از متد کمکی HubExtensions برای ارسال پیام بروزشده به هدف و به Bridge
                await _hubContext.SendMessageToTargetAndBridgeAsync(_logger, BridgeGroupName, targetType, targetId, updatedMessageDto);

                _logger.LogInformation("Notified target {TargetType} {TargetId} about pin change for message {MessageId} (isPinned={IsPinned})", targetType, targetId, messageId, isPinned);

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// ارسال پیام خصوصی از طریق SignalR Group
        /// </summary>
        public async Task SendPrivateMessageBroadcastAsync(MessageDto messageDto, long senderUserId, long receiverUserId)
        {
            var privateChatGroupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(senderUserId, receiverUserId);
            
            // تنظیم metadata
            messageDto.GroupType = "Private";
            messageDto.SenderUserId = senderUserId;
            messageDto.OwnerId = receiverUserId;
            
            _logger.LogInformation($"Broadcasting private message to group {privateChatGroupKey}");
            
            // ارسال به گروه SignalR
            await _hubContext.Clients.Group(privateChatGroupKey)
                .SendAsync("ReceiveMessage", messageDto);
            
            // ارسال به Bridge
            await _hubContext.Clients.Group(BridgeGroupName)
                .SendAsync("ReceiveMessage", messageDto);
            
            // ذخیره‌سازی آخرین پیام در Redis
            await _redisLastMessage.SetLastMessageAsync("private", privateChatGroupKey, new ChatMessageDto
            {
                Text = messageDto.MessageText?.MessageTxt,
                SentAt = messageDto.MessageDateTime,
                SenderName = messageDto.SenderUser?.NameFamily
            });
        }

        /// <summary>
        /// ارسال پیام سیستمی به گروههای مختلف
        /// </summary>
        public async Task SendSystemMessageBroadcastAsync(
            MessageDto messageDto, 
            EnumMessageType messageType, 
            long? targetGroupId = null,
            List<long>? specificUserIds = null)
        {
            string signalRGroup = messageType switch
            {
                // پیام در گروه خاص
                EnumMessageType.Group when targetGroupId.HasValue 
                    => $"ClassGroup_{targetGroupId.Value}",
                    
                // پیام در کانال خاص
                EnumMessageType.Channel when targetGroupId.HasValue 
                    => $"ChannelGroup_{targetGroupId.Value}",
                    
                // پیام به همه دانشجویان
                EnumMessageType.AllStudents 
                    => "role_students",
                    
                // پیام به همه معلمان
                EnumMessageType.AllTeachers 
                    => "role_teachers",
                    
                // پیام به همه پرسنل
                EnumMessageType.AllPersonel 
                    => "role_personnel",
                    
                _ => null
            };
            
            if (signalRGroup != null)
            {
                // ارسال به یک گروه
                await _hubContext.Clients.Group(signalRGroup)
                    .SendAsync("ReceiveSystemMessage", messageDto);
            }
            else if (messageType == EnumMessageType.Private && specificUserIds != null)
            {
                // ارسال به افراد خاص (پیامهای انبوه خصوصی)
                foreach (var userId in specificUserIds)
                {
                    var systemChatKey = PrivateChatHelper.GenerateSystemChatGroupKey(userId);
                    await _hubContext.Clients.Group(systemChatKey)
                        .SendAsync("ReceiveSystemMessage", messageDto);
                }
            }
        }

    }
}
