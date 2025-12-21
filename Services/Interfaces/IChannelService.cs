using Messenger.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IChannelService
    {
        Task<ChannelDto> CreateChannelAsync(long creatorUserId, string channelName, string channelTitle);
        Task<ChannelDto?> GetChannelByIdAsync(long userId, long channelId);

        /// <summary>
        /// برای استفاده داخلی سرویس‌ها
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        Task<ChannelDto?> GetChannelByIdInternalAsync(long channelId);

        Task<IEnumerable<ChannelDto>> GetAllChanneAsync();
        Task<IEnumerable<ChannelDto>> GetUserChannelsAsync(long userId);
        Task UpdateChannelInfoAsync(long channelId, string newName, string newTitle); // Requires permission check
        Task DeleteChannelAsync(long channelId); // Requires permission check

        // Channel Members
        Task AddUserToChannelAsync(long channelId, long userIdToAdd, long addedByUserId); // Requires permission check
        Task RemoveUserFromChannelAsync(long channelId, long userIdToRemove, long removedByUserId); // Requires permission check
        Task<IEnumerable<UserDto>> GetChannelMembersAsync(long userId, long channelId);

        /// <summary>
        /// برای استفاده داخلی سرویس‌ها
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        Task<IEnumerable<UserDto>> GetChannelMembersInternalAsync(long channelId);
        Task<bool> IsUserMemberOfChannelAsync(long userId, long channelId);

        // Search (Optional)
        // Task<IEnumerable<ChannelDto>> SearchChannelsAsync(string query);
    }
}

