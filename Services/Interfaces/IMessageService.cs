using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Tools;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IMessageService
    {
        // Sending Messages
        Task<MessageDto> SendPrivateMessageAsync(long senderUserId, long receiverUserId, string messageText, List<long>? files = null, long? replyToMessageId = null, bool isPortalMessage = false);


        // Task<MessageDto> SendChannelMessageAsync(long senderUserId, long channelId, string messageText, List<long>? files = null, long? replyToMessageId = null);

        /// <summary>
        /// گروه و کانال باهم یکجا شدند
        /// </summary>
        /// <param name="senderUserId"></param>
        /// <param name="classId"></param>
        /// <param name="groupType"></param>
        /// <param name="messageText"></param>
        /// <param name="files"></param>
        /// <param name="replyToMessageId"></param>
        /// <returns></returns>
        Task<MessageDto> SendGroupMessageAsync(long senderUserId, string classId, string groupType,
            string messageText, List<long>? files = null, long? replyToMessageId = null, bool isPin = false, bool isPortalMessage = false);

        /// <summary>
        /// ارسال به همه- گروه یا کانال یا افراد
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="messageText"></param>
        /// <param name="isPin"></param>
        /// <returns></returns>
        Task<MessageDto> SendMessageToAllAsync(long userId, EnumMessageType messageType, string messageText, bool isPin = true, bool isPortalMessage = false);

        // Retrieving Messages
        Task<MessageDto> GetMessageByIdAsync(long userId,long messageId);
        Task<PrivateChatDto> GetPrivateMessagesAsync(long currentUserId, long otherUserId, int pageSize, long messageId = 0, bool loadOlder = false, bool loadBothDirections = false);
        Task<IEnumerable<MessageDto>> GetChannelMessagesAsync(long channelId, long currentUserId, int pageNumber, int pageSize);

        Task<IEnumerable<MessageDto>> GetChatMessages(long chatId,
        string chatType, long currentUserId, int pageNumber, int pageSize,
            long messageId, bool loadOlder = false, bool loadBothDirections = false);

        Task<PrivateChatDto> GetPrivateChatMessagesAsync(long conversationId, long currentUserId, int pageSize,
            long messageId, bool loadOlder = false, bool loadBothDirections = false);

        Task<IEnumerable<MessageDto>> GetChatPinnedMessagesAsync(long classId, string chatType, long currentUserId, int pageSize);

        Task<IEnumerable<MessageFoulReportModelDto>> GetReportedMessagesAsync(long classId, string chatType, long currentUserId, int pageNumber, int pageSize,bool scope = false);

        //Task<List<Message>> GetClassGroupMessagesAsync(long classId, long userId, DateTime? lastReadMessageDateTime = null, int pageSize = 50);
        //Task<List<Message>> GetChannelMessagesAsync(long channelId, long userId, DateTime? lastReadMessageDateTime = null, int pageSize = 50);

        // متد برای دریافت آخرین زمان خوانده شدن پیام توسط کاربر در یک چت
        Task<DateTime?> GetLastReadMessageDateTimeAsync(long userId, long targetId, string groupType);
        Task<long> GetLastReadMessageIdFromSqlAsync(long userId, long targetId, string groupType);


        // Message Status & Actions
        Task<long?> MarkMessageAsReadAsync(long messageId, long userId, long targetId, string groupTpe);
        Task<IEnumerable<MessageReadDto>> GetMessageReadStatusAsync(long messageId);
        Task PinMessageAsync(long userId, long messageId, bool isPinned); // Context (channel/private/group) might be needed
        Task<ActionMessageDto?> HideMessageAsync(long messageId, long userId, long groupId, string groupType, bool isPortalMessage);

        // Reporting & Saving
        Task<MessageFoulReportDto> ReportMessageAsync(long messageId, long reporterUserId, string reason);
        Task SaveMessageAsync(long messageId, long userId);
        Task<IEnumerable<MessageSavedDto>> GetSavedMessagesAsync(long userId);
        Task DeleteSavedMessageAsync(long messageSavedId, long userId); // Ensure user owns the saved message


        // Edit
        Task<MessageDto> EditMessageAsync(long messageId, long editorUserId, string groupId, string groupType, string? newMessageText = null,
            List<long>? fileIds = null, List<long>? fileIdsToRemove = null);

        Task<MessageDto> EditChannelMessageAsync(long messageId, long editorUserId, string groupId, string? newMessageText = null,
            List<long>? fileIds = null, List<long>? fileIdsToRemove = null);

        Task<MessageDto> EditPrivateMessageAsync(long senderUserId, long messageId, long receiverUserId, string messageText,
            List<IFormFile>? files = null, long? replyToMessageId = null);

        // Delete
        //Task DeleteChatMessageAsync(long senderUserId, long classId, long messageId,string chatType);
        //Task DeleteChannelMessageAsync(long senderUserId, long classId, long messageId);
        //Task DeletePrivateMessageAsync(long senderUserId, long messageId);

        // redis

        Task<int> GetUnreadCountAsync(long userId, string groupType, long targetId);
        Task<List<UnreadMessageDto>> GetAllUnreadMessageInChat(long userId, long targetId, string groupType);
        Task<int> CalculateUnreadCountFromSqlAsync(long userId, long targetId, string groupType);

        // Private Chats & System Messages
        Task<IEnumerable<PrivateChatItemDto>> GetUserPrivateChatsAsync(long userId);
        Task<long> GetOtherUserIdInPrivateChat(string conversationId, long currentUserId);
    }
}

