using Messenger.DTOs;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserDto?> GetUserByIdAsync(long userId, ClaimsPrincipal userClaims = null, bool useInternalService = false);
        Task<IEnumerable<UserDto>> SearchUsersAsync(string query); // Basic search by name/dept etc.

        // Blocking Users
        Task BlockUserAsync(long blockerUserId, long blockedUserId, string? comment = null);
        Task UnblockUserAsync(long creatorUserId, long blockedUserId);
        Task<IEnumerable<BlockedUserDto>> GetBlockedUsersAsync(long userId);

        Task<bool> UserExistsAndHasRoleAsync(long userId, string roleName);

        // User Profile (Potentially)
        // Task UpdateUserProfileAsync(int userId, UserProfileUpdateDto profileData);
        // Task UpdateProfilePictureAsync(int userId, byte[] pictureData);

        // User Presence (Optional)
        // Task SetUserOnlineStatusAsync(int userId, bool isOnline);
        // Task<UserStatusDto> GetUserOnlineStatusAsync(int userId);
    }
}

