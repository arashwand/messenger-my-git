using Messenger.API.ServiceHelper;
using Messenger.API.ServiceHelper.Interfaces;
using Messenger.DTOs; // Assuming ChatMessageDto and other DTOs are here
using Messenger.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Messenger.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class RedisServicesController : ControllerBase
    {
        private readonly IRedisUserStatusService _redisUserStatusService;
        private readonly RedisLastMessageService _redisLastMessageService;
        private readonly ILogger<RedisServicesController> _logger;

        public RedisServicesController(
            IRedisUserStatusService redisUserStatusService,
            RedisLastMessageService redisLastMessageService,
            ILogger<RedisServicesController> logger)
        {
            _redisUserStatusService = redisUserStatusService;
            _redisLastMessageService = redisLastMessageService;
            _logger = logger;
        }

        // --- IRedisUserStatusService Endpoints ---

        // GetOnlineUsersAsync was mentioned in ChatHub, let's assume it's useful
        [HttpGet("onlineUsers/{groupKey}")]
        public async Task<IActionResult> GetOnlineUsers(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey))
            {
                return BadRequest("GroupKey cannot be null or empty.");
            }
            try
            {
                var onlineUserIds = await _redisUserStatusService.GetOnlineUsersAsync(groupKey);
                return Ok(onlineUserIds);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting online users for groupKey {GroupKey}", groupKey);
                return StatusCode(500, "Internal server error");
            }
        }

        // Example: Get user group keys (if needed by WebApp)
        [HttpGet("userGroupKeys/{userId}")]
        public async Task<IActionResult> GetUserGroupKeys(long userId)
        {
            if (userId <= 0)
            {
                return BadRequest("Invalid userId.");
            }
            try
            {
                var groupKeys = await _redisUserStatusService.GetUserGroupKeysAsync(userId);
                return Ok(groupKeys);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting user group keys for userId {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }


        // SetUserOnlineAsync, SetUserOfflineAsync are likely called internally by ChatHub upon connect/disconnect.
        // Exposing them via API might be a security risk or unnecessary unless specific scenarios demand it.
        // If WebApp needs to trigger these, ensure proper validation and authorization.

        // --- IRedisLastMessageService Endpoints ---

        [HttpGet("lastMessage/{groupType}/{groupId}")]
        public async Task<IActionResult> GetLastMessage(string groupType, string groupId)
        {
            if (string.IsNullOrEmpty(groupType) || string.IsNullOrEmpty(groupId))
            {
                return BadRequest("GroupType and GroupId cannot be null or empty.");
            }
            try
            {
                // Assuming GetLastMessageAsync exists and returns a suitable DTO (e.g., ChatMessageDto)
                var lastMessage = await _redisLastMessageService.GetLastMessageAsync(groupType, groupId);
                if (lastMessage == null)
                {
                    return NotFound();
                }
                return Ok(lastMessage);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting last message for group {GroupType}/{GroupId}", groupType, groupId);
                return StatusCode(500, "Internal server error");
            }
        }

        // SetLastMessageAsync is likely called by ChatHub after a new message is processed.
        // Exposing it via API is probably not needed for WebApp.

        // Note: More endpoints can be added based on the specific needs of HomeController in WebApp.
        // For instance, if HomeController needs to update user status or cache group keys directly
        // (which is less likely, as ChatHub should handle this), those endpoints would go here.
    }
}
