using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Messenger.DTOs;
using Messenger.Tools;

namespace Messenger.API.Hubs
{
    public static class HubExtensions
    {

        #region new method 
        //TODO: بجای دو متد زیر میتونیم از یک متد استفاده کنیم و نوع پیام رو به صورت پارامتر بفرستیم


        public static async Task UnifiedSendAsync(
            this Hub hub,
            ILogger logger,
            EnumMessageType targetType,
            string targetKey,               // userId OR groupKey
            string method,
            object[] args,
            string bridgeGroupName,
            bool isBridgeSender = false)
        {
            try
            {
                logger.LogInformation("UnifiedSendAsync: type={Type}, key={Key}, method={Method}, isBridge={Bridge}",
                    targetType, targetKey, method, isBridgeSender);

                // ارسال به هدف اصلی
                if (targetType == EnumMessageType.Private)
                {
                    logger.LogInformation("Sending to User({UserId})", targetKey);
                    await hub.Clients.User(targetKey).SendAsync(method, args);
                }
                else
                {
                    logger.LogInformation("Sending to Group({GroupKey}) excluding sender", targetKey);
                    await hub.Clients.GroupExcept(targetKey, hub.Context.ConnectionId).SendAsync(method, args);
                }

                // ارسال به Bridge (همیشه یک اتصال واحد)
                if (!isBridgeSender)
                {
                    logger.LogInformation("Sending to the Bridge group({BridgeGroup})", bridgeGroupName);
                    await hub.Clients.Group(bridgeGroupName).SendAsync(method, args);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UnifiedSendAsync failed for method {Method}, key {Key}", method, targetKey);
            }
        }


        #endregion


        /// <summary>
        /// پیام را به گروه و واسط‌ها پخش می‌کند
        /// </summary>
        public static async Task BroadcastToGroupAndBridgeAsync(this Hub hub, ILogger logger, string bridgeGroupName,
            string groupKey, string groupMethod, object[] groupArgs, string? bridgeMethod = null, object[]? bridgeArgs = null, bool isBridgeSender = false)
        {
            try
            {
                logger.LogInformation("BroadcastToGroupAndBridgeAsync: method={Method}, isBridge={IsBridge}, groupKey={GroupKey}", 
                    groupMethod, isBridgeSender, groupKey);

                // ارسال به اعضای گروه (به جز فرستنده)
                if (isBridgeSender)
                {
                    // Bridge فرستنده است - فقط به کلاینت‌های موبایل ارسال کن
                    logger.LogInformation("Sending to GroupExcept({GroupKey}) excluding bridge sender", groupKey);
                    await hub.Clients.GroupExcept(groupKey, hub.Context.ConnectionId).SendAsync(groupMethod, groupArgs);
                }
                else
                {
                    // کلاینت موبایل فرستنده است - به همه به جز فرستنده
                    logger.LogInformation("Sending to GroupExcept({GroupKey}) excluding mobile sender", groupKey);
                    await hub.Clients.GroupExcept(groupKey, hub.Context.ConnectionId).SendAsync(groupMethod, groupArgs);
                }
                
                // ارسال به تمام Bridge ها (اگر bridgeMethod مشخص شده باشد)
                if (!string.IsNullOrEmpty(bridgeMethod))
                {
                    logger.LogInformation("Sending to ALL bridges: Group({BridgeGroup})", bridgeGroupName);
                    await hub.Clients.Group(bridgeGroupName).SendAsync(bridgeMethod, bridgeArgs ?? groupArgs);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BroadcastToGroupAndBridgeAsync failed for groupKey {GroupKey} method {Method}", groupKey, groupMethod);
            }
        }

        /// <summary>
        /// پیام را به کاربر و واسط‌ها اطلاع می‌دهد
        /// </summary>
        public static async Task NotifyUserAndBridgeAsync(this Hub hub, ILogger logger, string bridgeGroupName,
            long userId, string method, object[] args, bool isBridgeSender = false)
        {
            try
            {
                logger.LogInformation("NotifyUserAndBridgeAsync: method={Method}, userId={UserId}, isBridge={IsBridge}", 
                    method, userId, isBridgeSender);

                // ارسال به کاربر
                logger.LogInformation("Sending to User({UserId})", userId);
                await hub.Clients.User(userId.ToString()).SendAsync(method, args);
                
                // ارسال به تمام Bridge ها
                logger.LogInformation("Sending to ALL bridges: Group({BridgeGroup})", bridgeGroupName);
                await hub.Clients.Group(bridgeGroupName).SendAsync(method, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NotifyUserAndBridgeAsync failed for user {UserId} method {Method}", userId, method);
            }
        }

        /// <summary>
        /// پیام را به چندین گروه و واسط‌ها پخش می‌کند.
        /// الگو برداری شده از BroadcastToGroupAndBridgeAsync برای سناریوی ارسال مدیریتی.
        /// - به اعضای هر گروه (به جز فرستنده) پیام معمولی (ReceiveMessage) می‌رسد.
        /// - به Bridge ها payload کامل جهت تبدیل و توزیع در وب می‌رسد.
        /// args انتظار دارد ساختار: [MessageDto, EnumMessageType, targetIds]
        /// </summary>
        public static async Task BroadcastToGroupsAndBridgeAsync(this IHubContext<ChatHub> hubContext, ILogger logger, string bridgeGroupName,
            IEnumerable<string> groupKeys, string bridgeMethod, object[] bridgeArgs)
        {
            try
            {
                var groupCount = 0;
                foreach (var groupKey in groupKeys)
                {
                    groupCount++;
                    // برای کلاینت‌های موبایل: فقط خود پیام را مانند ارسال معمولی بفرست
                    if (bridgeArgs.Length > 0)
                    {
                        var messageDto = bridgeArgs[0];
                        await hubContext.Clients.Group(groupKey).SendAsync("ReceiveMessage", messageDto);
                    }
                }
                logger.LogInformation("BroadcastToGroupsAndBridgeAsync (enhanced): sent to {Count} groups, bridgeMethod={BridgeMethod}", groupCount, bridgeMethod);

                // ارسال به Bridge با آرگومان کامل برای اینکه HubConnectionManager بتواند آن را گسترش دهد
                await hubContext.Clients.Group(bridgeGroupName).SendAsync(bridgeMethod, bridgeArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BroadcastToGroupsAndBridgeAsync failed for bridgeMethod {Method}", bridgeMethod);
            }
        }

        /// <summary>
        /// پیام را به چندین کاربر و واسط‌ها اطلاع می‌دهد (تغییر: ارسال ساده به هر کاربر + payload کامل به Bridge)
        /// </summary>
        public static async Task BroadcastToUsersAndBridgeAsync(this IHubContext<ChatHub> hubContext, ILogger logger, string bridgeGroupName,
            IEnumerable<string> userIds, string bridgeMethod, object[] bridgeArgs)
        {
            try
            {
                var userCount = 0;
                foreach (var userId in userIds)
                {
                    userCount++;
                    if (bridgeArgs.Length > 0)
                    {
                        var messageDto = bridgeArgs[0];
                        await hubContext.Clients.User(userId).SendAsync("ReceiveMessage", messageDto);
                    }
                }
                logger.LogInformation("BroadcastToUsersAndBridgeAsync (enhanced): sent to {Count} users, bridgeMethod={BridgeMethod}", userCount, bridgeMethod);

                // ارسال payload کامل به Bridge
                await hubContext.Clients.Group(bridgeGroupName).SendAsync(bridgeMethod, bridgeArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BroadcastToUsersAndBridgeAsync failed for bridgeMethod {Method}", bridgeMethod);
            }
        }

        /// <summary>
        /// ارسال پیام به یک هدف خاص (کاربر یا گروه) و واسط‌ها
        /// برای پیام‌های خصوصی یا گروهی استفاده می‌شود
        /// </summary>
        public static async Task SendMessageToTargetAndBridgeAsync(this IHubContext<ChatHub> hubContext, ILogger logger, string bridgeGroupName,
            string targetType, long targetId, MessageDto messageDto)
        {
            try
            {
                string groupKey;
                
                if (targetType == "Private" || targetType == ConstChat.PrivateType)
                {
                    // برای Private: محاسبه groupKey از روی sender و receiver
                    var senderId = messageDto.SenderUserId;
                    var receiverId = messageDto.OwnerId > 0 ? messageDto.OwnerId : targetId;
                    groupKey = PrivateChatHelper.GeneratePrivateChatGroupKey(senderId, receiverId);
                    
                    messageDto.GroupType = "Private";
                    messageDto.ChatKey = groupKey;
                    // Note: groupId در Bridge تنظیم میشود چون برای هر کاربر متفاوت است
                    
                    logger.LogInformation($"Private message: sender={senderId}, receiver={receiverId}, groupKey={groupKey}");
                }
                else if (targetType == ConstChat.ClassGroupType || targetType == ConstChat.ChannelGroupType)
                {
                    // برای Group/Channel
                    groupKey = GenerateSignalRGroupKey.GenerateKey((int)targetId, targetType);
                    messageDto.GroupId = targetId;
                    messageDto.GroupType = targetType;
                    messageDto.ChatKey = groupKey;
                    
                    logger.LogInformation($"Group message: targetId={targetId}, groupKey={groupKey}");
                }
                else
                {
                    logger.LogError("Unsupported targetType {TargetType} in SendMessageToTargetAndBridgeAsync", targetType);
                    return;
                }
                
                // ارسال به گروه (موبایل + وب)
                await hubContext.Clients.Group(groupKey)
                    .SendAsync("ReceiveMessage", messageDto);
                
                // ارسال به Bridge (برای کلاینتهای وب)
                await hubContext.Clients.Group(bridgeGroupName)
                    .SendAsync("ReceiveMessage", messageDto);
                    
                logger.LogInformation($"Message sent to group {groupKey} and bridge");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SendMessageToTargetAndBridgeAsync failed for targetType={Type}, targetId={Id}", targetType, targetId);
                throw;
            }
        }
    }
}
