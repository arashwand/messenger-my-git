using Messenger.DTOs;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.Services.Interfaces;

namespace Messenger.API.ServiceHelper
{
    public class ChannelHandler : IChannelHandler
    {
        private readonly IChannelService _channelServiceClient;

        public ChannelHandler(IChannelService channelServiceClient)
        {
            _channelServiceClient = channelServiceClient ?? throw new ArgumentNullException(nameof(channelServiceClient));
        }

        public async Task<ChannelDto> CreateChannelAsync(int creatorUserId, string channelName, string channelTitle)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Channel name cannot be empty.", nameof(channelName));
            if (string.IsNullOrWhiteSpace(channelTitle))
                throw new ArgumentException("Channel title cannot be empty.", nameof(channelTitle));

            return await _channelServiceClient.CreateChannelAsync(creatorUserId, channelName, channelTitle);
        }

        public async Task<ChannelDto?> GetChannelByIdAsync(int channelId)
        {
            if (channelId <= 0)
                throw new ArgumentException("Channel ID must be greater than zero.", nameof(channelId));

            return await _channelServiceClient.GetChannelByIdInternalAsync(channelId);
        }

        public async Task<IEnumerable<ChannelDto>> GetUserChannelsAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than zero.", nameof(userId));

            return await _channelServiceClient.GetUserChannelsAsync(userId);
        }

        public async Task AddUserToChannelAsync(int channelId, int userIdToAdd, int addedByUserId)
        {
            if (channelId <= 0)
                throw new ArgumentException("Channel ID must be greater than zero.", nameof(channelId));
            if (userIdToAdd <= 0)
                throw new ArgumentException("User ID to add must be greater than zero.", nameof(userIdToAdd));
            if (addedByUserId <= 0)
                throw new ArgumentException("Added by user ID must be greater than zero.", nameof(addedByUserId));

            await _channelServiceClient.AddUserToChannelAsync(channelId, userIdToAdd, addedByUserId);
        }

        public async Task<bool> IsUserMemberOfChannelAsync(int userId, int channelId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than zero.", nameof(userId));
            if (channelId <= 0)
                throw new ArgumentException("Channel ID must be greater than zero.", nameof(channelId));

            return await _channelServiceClient.IsUserMemberOfChannelAsync(userId, channelId);
        }
    }
}
