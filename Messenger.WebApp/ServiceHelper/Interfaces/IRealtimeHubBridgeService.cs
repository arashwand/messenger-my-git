using Messenger.DTOs;

namespace Messenger.WebApp.ServiceHelper.Interfaces
{
    public interface IRealtimeHubBridgeService
    {
        bool IsConnected { get; }
        Task ConnectAsync(string token);
        Task DisconnectAsync();

        // Methods to forward actions to the main API Hub
        Task AnnounceUserPresenceAsync(long userId);
        Task AnnounceUserDepartureAsync(long userId);
        Task SendTypingSignalAsync(long userId, long groupId, string groupType);
        Task SendStopTypingSignalAsync(long userId, long groupId, string groupType);
        Task MarkMessageAsReadAsync(long userId, long groupId, string groupType, long messageId);
        Task MarkAllMessagesAsReadAsync(long userId, long groupId, string groupType);
        Task SendMessageAsync(SendMessageRequestDto request);
        Task EditMessageAsync(EditMessageRequestDto request);
        Task<List<object>> GetUsersWithStatusAsync(long groupId, string groupType);
        Task SendHeartbeatAsync(long userId);
        Task RequestUnreadCounts(long userId);
    }
}
