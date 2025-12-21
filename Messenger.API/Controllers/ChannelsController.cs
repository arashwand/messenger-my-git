using Messenger.API.RequestDTOs; // Added for ClaimsPrincipal
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Messenger.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Secure the entire controller
    public class ChannelsController : ControllerBase
    {
        private readonly IChannelService _channelService;
        private readonly ILogger<ChannelsController> _logger;

        public ChannelsController(IChannelService channelService, ILogger<ChannelsController> logger)
        {
            _channelService = channelService;
            _logger = logger;

        }

        // Helper to get current user ID from JWT claims
        private long GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
            {
                return userId;
            }
            // This should not happen if [Authorize] is working correctly and token is valid
            return 0;
        }


        /// <summary>
        /// ساخت کانال - فقط توسط ادمین
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<ChannelDto>> CreateChannel([FromBody] CreateChannelRequest request)
        {
            var creatorUserId = GetCurrentUserId();
            if (creatorUserId <= 0) return Unauthorized();

            try
            {
                var channelDto = await _channelService.CreateChannelAsync(creatorUserId, request.ChannelName, request.ChannelTitle);
                // Return CreatedAtAction pointing to GetChannelById if desired
                return Ok(channelDto);
            }
            catch (Exception ex)
            {
                // Log exception
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// گرفتن جزییات کانال با ایدی
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        [HttpGet("{channelId}")]
        public async Task<ActionResult<ChannelDto>> GetChannelById(long channelId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId <= 0) return Unauthorized();

                var channel = await _channelService.GetChannelByIdAsync(userId, channelId);
                if (channel == null) return NotFound();
                return Ok(channel);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {

                throw;
            }

        }

        [HttpGet("all")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<IEnumerable<ChannelDto>>> GetAllChannel()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user a member of this channel?
            //bool isMember = await _channelService.IsUserMemberOfChannelAsync(userId, channelId);
            //if (!isMember) return Forbid();

            var channel = await _channelService.GetAllChanneAsync();
            if (channel == null) return NotFound();
            return Ok(channel);
        }


        /// <summary>
        /// کانالهایی که کاربر عضو است را بهش میده
        /// </summary>
        /// <returns></returns>
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<ChannelDto>>> GetMyChannels()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var channels = await _channelService.GetUserChannelsAsync(userId);
            return Ok(channels);
        }


        /// <summary>
        /// بروزرسانی کانال
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpPut("{channelId}")]
        public async Task<IActionResult> UpdateChannelInfo(long channelId, [FromBody] UpdateChannelRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user an admin of this channel?

            try
            {
                await _channelService.UpdateChannelInfoAsync(channelId, request.ChannelName, request.ChannelTitle);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Handle specific exceptions like 'not found' or 'forbidden'
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// حذف کانال
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpDelete("{channelId}")]
        public async Task<IActionResult> DeleteChannel(long channelId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user the creator or admin of this channel?

            try
            {
                await _channelService.DeleteChannelAsync(channelId);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Handle specific exceptions
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// مشاهده اعضال کانال
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        [HttpGet("{channelId}/members")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetChannelMembers(long channelId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                var members = await _channelService.GetChannelMembersAsync(userId, channelId);
                return Ok(members);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// بررسی اینکه این کاربر  متعلق به گروه مورد نظر است یا خیر
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet("{channelId}/members/{userId}/is-member")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<MemberDto>> IsUserMemberOfChannelAsync(long channelId, long userId)
        {
            var _userId = GetCurrentUserId();
            if (_userId <= 0) return Unauthorized();
            // Add permission check: Is user a member of this channel?

            try
            {
                bool isMember = await _channelService.IsUserMemberOfChannelAsync(userId, channelId);
                if (!isMember) return Forbid();

                _logger.LogInformation("User {UserId} membership status in class group {ClassId}: {IsMember}", userId, channelId, isMember);

                return Ok(new MemberDto
                {
                    UserId = userId,
                    ClassId = channelId,
                    IsMember = isMember
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking membership for user {UserId} in class group {ClassId}", userId, channelId);
                return StatusCode(500, new { error = "An unexpected error occurred while checking membership." });
            }
        }


        /// <summary>
        /// اضافه کردن کاربر به کانال
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="userIdToAdd"></param>
        /// <returns></returns>
        [HttpPost("{channelId}/members/{userIdToAdd}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> AddUserToChannel(long channelId, int userIdToAdd)
        {
            var addedByUserId = GetCurrentUserId();
            if (addedByUserId <= 0) return Unauthorized();
            // Add permission check: Does addedByUserId have permission to add members?

            try
            {
                await _channelService.AddUserToChannelAsync(channelId, userIdToAdd, addedByUserId);
                return NoContent(); // Or Ok() if preferred
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// حذف کاربر از کانال
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="userIdToRemove"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpDelete("{channelId}/members/{userIdToRemove}")]
        public async Task<IActionResult> RemoveUserFromChannel(long channelId, int userIdToRemove)
        {
            var removedByUserId = GetCurrentUserId();
            if (removedByUserId <= 0) return Unauthorized();
            // Add permission check: Does removedByUserId have permission to remove members (or is it self-removal)?

            try
            {
                await _channelService.RemoveUserFromChannelAsync(channelId, userIdToRemove, removedByUserId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

