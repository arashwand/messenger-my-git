using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Messenger.Services.Services
{
    public class ChannelService : IChannelService
    {
        private readonly IEMessengerDbContext _context;
        private readonly IUserService _userService; // For user checks
        private readonly ILogger<ChannelService> _logger; // For logging (optional)

        public ChannelService(IEMessengerDbContext context, IUserService userService, ILogger<ChannelService> logger)
        {
            _context = context;
            _logger = logger;
            _userService = userService;
            Console.WriteLine("ChannelService initialized (placeholder implementation)");
        }

        public async Task<ChannelDto> CreateChannelAsync(long creatorUserId, string channelName, string channelTitle)
        {
            Console.WriteLine($"User {creatorUserId} attempting to create channel '{channelName}'");
            // 1. Validate creator user exists
            // 2. Create Channel entity
            var channelEntity = new Channel
            {/* Placeholder Entity */
                ChannelName = channelName,
                ChannelTitle = channelTitle,
                CreateDate = DateTime.UtcNow,
                CreatorUserId = creatorUserId
            };
            _context.Channels.Add(channelEntity);
            await _context.SaveChangesAsync(); // Save to get ChannelId


            // 3. Add creator as the first member (ChannelMembers)
            var memberEntity = new ChannelMember
            {/* Placeholder Entity */
                ChannelId = channelEntity.ChannelId,
                UserId = creatorUserId,
                CreateDate = DateTime.UtcNow
                // Role = "Admin" // Optional: Define roles
            };
            _context.ChannelMembers.Add(memberEntity);
            await _context.SaveChangesAsync();

            return new ChannelDto
            {
                ChannelId = channelEntity.ChannelId,
                ChannelName = channelEntity.ChannelName,
                ChannelTitle = channelEntity.ChannelTitle,
                CreateDate = channelEntity.CreateDate
            };
        }

        public async Task<ChannelDto?> GetChannelByIdAsync(long userId, long channelId)
        {
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID: {UserId} not found.", userId);
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
            {
                // Check if user is a member of the class group
                var isMember = await IsUserMemberOfChannelAsync(userId, channelId);
                if (!isMember)
                {
                    _logger.LogWarning("User {UserId} does not have access to channel {ChannelId}.", userId, channelId);
                    throw new UnauthorizedAccessException("User does not have access to this channel.");
                }
            }

            Console.WriteLine($"Getting channel by ID: {channelId}");
            return await ChannelByIdAsync(channelId);
        }
        public async Task<ChannelDto?> GetChannelByIdInternalAsync(long channelId)
        {
            Console.WriteLine($"Getting channel by ID: {channelId}");
            return await ChannelByIdAsync(channelId);
        }

        private async Task<ChannelDto?> ChannelByIdAsync(long channelId)
        {            
            Console.WriteLine($"Getting channel by ID: {channelId}");
            // Fetch channel from DB  
            var channelEntity = await _context.Channels.FindAsync(channelId);
            if (channelEntity == null) return null;

            return new ChannelDto
            {
                ChannelId = channelEntity.ChannelId,
                ChannelName = channelEntity.ChannelName,
                ChannelTitle = channelEntity.ChannelTitle,
                CreateDate = channelEntity.CreateDate
            };
        }

        public async Task<IEnumerable<ChannelDto>> GetAllChanneAsync()
        {
            try
            {
                Console.WriteLine($"Getting all channel");
                // Fetch channel from DB  
                var channels = await _context.Channels.ToListAsync();
                if (channels == null) return null;

                return channels.Select(s => new ChannelDto
                {
                    ChannelId = s.ChannelId,
                    ChannelName = s.ChannelName,
                    ChannelTitle = s.ChannelTitle,
                    CreateDate = s.CreateDate,
                    CreatorUserId = s.CreatorUserId
                }).ToList();
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<IEnumerable<ChannelDto>> GetUserChannelsAsync(long userId)
        {
            try
            {
                Console.WriteLine($"Getting channels for user {userId}");

                var channelEntities = await _context.Channels
            .Join(_context.ChannelMembers,
                channel => channel.ChannelId,
                member => member.ChannelId,
                (channel, member) => new { channel, member })
            .Where(x => x.member.UserId == userId)
            .Select(x => new ChannelDto
            {
                ChannelId = x.channel.ChannelId,
                ChannelName = x.channel.ChannelName,
                ChannelTitle = x.channel.ChannelTitle
            })
            .ToListAsync();

                return channelEntities;

            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task UpdateChannelInfoAsync(long channelId, string newName, string newTitle)
        {
            Console.WriteLine($"Updating info for channel {channelId}");
            // 1. Find channel
            var channelEntity = await _context.Channels.FindAsync(channelId);
            if (channelEntity == null) throw new Exception("Channel not found");
            // 2. Check permissions (e.g., is user an admin of the channel?)
            // 3. Update properties
            channelEntity.ChannelName = newName;
            channelEntity.ChannelTitle = newTitle;
            // 4. Save changes
            await _context.SaveChangesAsync();
        }

        public async Task DeleteChannelAsync(long channelId)
        {
            Console.WriteLine($"Deleting channel {channelId}");
            // 1. Find channel
            var channelEntity = await _context.Channels.FindAsync(channelId);
            if (channelEntity == null) throw new Exception("Channel not found");
            // 2. Check permissions (e.g., is user the creator or admin?)
            // 3. Remove channel (and related entities like ChannelMessages, ChannelMembers - cascading delete?)
            // 4. Remove channle messags

            byte messageType = (byte)EnumMessageType.Channel;
            var channelMessages = await _context.Messages
                .Where(cm => cm.OwnerId == channelId && cm.MessageType == messageType)
                .Include(i => i.MessageFiles)
                .ToListAsync();

            var messageFiles = channelMessages.SelectMany(cm => cm.MessageFiles).ToList();
            _context.MessageFiles.RemoveRange(messageFiles);

            _context.Messages.RemoveRange(channelMessages);

            var channelMembers = await _context.ChannelMembers
                .Where(cm => cm.ChannelId == channelId)
                .ToListAsync();
            _context.ChannelMembers.RemoveRange(channelMembers);

            _context.Channels.Remove(channelEntity);
            await _context.SaveChangesAsync();

        }

        public async Task AddUserToChannelAsync(long channelId, long userIdToAdd, long addedByUserId)
        {
            Console.WriteLine($"User {addedByUserId} adding user {userIdToAdd} to channel {channelId}");
            // 1. Validate channel and users exist
            var channelEntity = await _context.Channels.FindAsync(channelId);
            if (channelEntity == null) throw new Exception("Channel not found");

            var userToAdd = await _userService.GetUserByIdAsync(userIdToAdd);
            if (userToAdd == null) throw new Exception("User to add not found");

            // 2. Check permissions of addedByUserId (e.g., is admin?)
            // 3. Check if userIdToAdd is already a member
            var chekcMember = await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userIdToAdd);
            if (chekcMember) throw new Exception("User is already a member of the channel");

            // 4. Create ChannelMembers entity

            _context.ChannelMembers.Add(new ChannelMember { ChannelId = channelId, UserId = userIdToAdd, CreateDate = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            // 5. Optionally, notify the user (e.g., via email or in-app notification)
        }

        public async Task RemoveUserFromChannelAsync(long channelId, long userIdToRemove, long removedByUserId)
        {
            Console.WriteLine($"User {removedByUserId} removing user {userIdToRemove} from channel {channelId}");
            // 1. Validate channel and users exist
            var channelEntity = await _context.Channels.FindAsync(channelId);
            if (channelEntity == null) throw new Exception("Channel not found");

            var userToAdd = await _userService.GetUserByIdAsync(userIdToRemove);
            if (userToAdd == null) throw new Exception("User to add not found");
            // 2. Check permissions of removedByUserId (e.g., is admin? or self-removal?)
            // 3. Find ChannelMembers entity
            var memberEntity = await _context.ChannelMembers.FirstOrDefaultAsync(cm => cm.ChannelId == channelId && cm.UserId == userIdToRemove);
            if (memberEntity != null)
            {
                _context.ChannelMembers.Remove(memberEntity);
                await _context.SaveChangesAsync();
            }
            // 4. Optionally, notify the user (e.g., via email or in-app notification)
        }

        public async Task<IEnumerable<UserDto>> GetChannelMembersAsync(long userId, long channelId)
        {
            Console.WriteLine($"Getting members for channel {channelId}");

            // 1. Check if requesting user is a member of the channel
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID: {UserId} not found.", userId);
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            if (user.RoleName != ConstRoles.Manager && user.RoleName != ConstRoles.Personel)
            {
                // Check if user is a member of the class group
                var isMember = await IsUserMemberOfChannelAsync(userId, channelId);
                if (!isMember)
                {
                    _logger.LogWarning("User {UserId} does not have access to channel {ChannelId}.", userId, channelId);
                    throw new UnauthorizedAccessException("User does not have access to this channel.");
                }
            }

            return await ChannelMembersAsync(channelId);
        }


        public async Task<IEnumerable<UserDto>> GetChannelMembersInternalAsync(long channelId)
        {
            _logger.LogInformation("Getting members for channel {ChannelId}", channelId);

            // 1. Check if requesting user is a member of the channel
            return await ChannelMembersAsync(channelId);
        }

        /// <summary>
        /// دریافت تعداد اعضای کانال
        /// </summary>
        public async Task<int> GetChannelMembersCountAsync(long channelId)
        {
            _logger.LogInformation("Getting member count for channel {ChannelId}", channelId);
            
            try
            {
                // Count members via ChannelMembers table
                var count = await _context.ChannelMembers
                    .Where(cm => cm.ChannelId == channelId)
                    .CountAsync();

                _logger.LogInformation("Channel {ChannelId} has {Count} members", channelId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member count for channel {ChannelId}", channelId);
                return 0;
            }
        }

        private async Task<IEnumerable<UserDto>> ChannelMembersAsync(long channelId)
        {
            _logger.LogInformation("Getting members for channel {ChannelId}", channelId);


            var userEntities = await _context.ChannelMembers
                .Where(w => w.ChannelId == channelId)
                .Include(u => u.User)
                .Select(ucg => ucg.User) // Select the User entity
                   .Where(u => u != null) // اطمینان از اینکه User وجود دارد
                   .ToListAsync();


            return userEntities.Select(u => new UserDto
            {
                // Assuming UserDto has properties like UserId, NameFamily, DeptName, etc.
                UserId = u.UserId,
                NameFamily = u.NameFamily,
                DeptName = u.DeptName,
                ProfilePicName = u.ProfilePicName,
                RoleFaName = u.RoleFaName,
                RoleName = u.RoleName,
                // Add other properties as needed
            });
        }

        public async Task<bool> IsUserMemberOfChannelAsync(long userId, long channelId)
        {
            _logger.LogInformation($"Checking if user {userId} is member of channel {channelId}");
            // Check if a ChannelMembers entry exists       
            return await _context.ChannelMembers.AnyAsync(cm => cm.UserId == userId && cm.ChannelId == channelId);
        }
    }
}

