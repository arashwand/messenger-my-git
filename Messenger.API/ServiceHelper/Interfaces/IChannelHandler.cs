using Messenger.DTOs;

namespace Messenger.API.ServiceHelper.Interfaces
{

    public interface IChannelHandler
    {
        Task<ChannelDto> CreateChannelAsync(int creatorUserId, string channelName, string channelTitle);
        Task<ChannelDto?> GetChannelByIdAsync(int channelId);
        Task<IEnumerable<ChannelDto>> GetUserChannelsAsync(int userId);
        Task AddUserToChannelAsync(int channelId, int userIdToAdd, int addedByUserId);
        Task<bool> IsUserMemberOfChannelAsync(int userId, int channelId);
    }
}
