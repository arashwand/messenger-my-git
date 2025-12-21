using Messenger.DTOs;

namespace Messenger.WebApp.ServiceHelper.Interfaces
{
    public interface IChannelServiceClient
    {
        Task<ChannelDto> CreateChannelAsync(long creatorUserId, string channelName, string channelTitle);
        Task<ChannelDto?> GetChannelByIdAsync(long channelId);
        Task<IEnumerable<ChannelDto>> GetAllChannelAsync();
        Task<IEnumerable<ChannelDto>> GetUserChannelsAsync(long userId);
        Task UpdateChannelInfoAsync(long channelId, string newName, string newTitle);
        Task DeleteChannelAsync(long channelId);
        Task AddUserToChannelAsync(long channelId, long userIdToAdd, long addedByUserId);
        Task RemoveUserFromChannelAsync(long channelId, long userIdToRemove, long removedByUserId);
        Task<IEnumerable<UserDto>> GetChannelMembersAsync(long channelId);
        Task<bool> IsUserMemberOfChannelAsync(long userId, long channelId);
    }
}
