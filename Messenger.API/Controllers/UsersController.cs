using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]
using System.Security.Claims; // Added for ClaimsPrincipal
using Messenger.Tools; // Added for ConstRoles
using Microsoft.Extensions.Logging; // Added for logging

namespace MessengerApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // Helper to get current user ID from JWT claims
        private int GetCurrentUserId()
        {
            //var userId = User.Identity.Name ?? throw new UnauthorizedAccessException("User not authenticated.");
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            // This should not happen if [Authorize] is working correctly and token is valid
            return 0;
        }

        // Helper to get current user role from JWT claims
        private string GetCurrentUserRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value ?? string.Empty;
        }


        [HttpGet("{userId}")]
        public async Task<ActionResult<UserDto>> GetUserById(long userId)
        {
            var user = await _userService.GetUserByIdAsync(userId, User);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpGet("search")]
        [Authorize(Roles = $"{ConstRoles.Manager},{ConstRoles.Personel}")]
        public async Task<ActionResult<IEnumerable<UserDto>>> SearchUsers([FromQuery] string query)
        {
            try
            {
                var currentUserRole = GetCurrentUserRole();
                _logger.LogInformation("User with role {Role} is searching for users with query: {Query}", currentUserRole, query);

                // Validate input: minimum 2 characters
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    _logger.LogWarning("Invalid search query: {Query}", query);
                    return BadRequest("Search query must be at least 2 characters.");
                }

                var users = await _userService.SearchUsersAsync(query);
                _logger.LogInformation("Search completed. Found {Count} users.", users?.Count() ?? 0);
                
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching users with query: {Query}", query);
                return StatusCode(500, "An error occurred while searching for users.");
            }
        }

        // --- Blocked Users ---

        [HttpGet("blocked")]
        public async Task<ActionResult<IEnumerable<BlockedUserDto>>> GetBlockedUsers()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            var blockedUsers = await _userService.GetBlockedUsersAsync(userId);
            return Ok(blockedUsers);
        }

        public record BlockUserRequest(string? Comment);
        [HttpPost("block/{userIdToBlock}")]
        public async Task<IActionResult> BlockUser(int userIdToBlock, [FromBody] BlockUserRequest? request)
        {
            var blockerUserId = GetCurrentUserId();
            if (blockerUserId <= 0) return Unauthorized();
            if (blockerUserId == userIdToBlock) return BadRequest("Cannot block yourself.");

            try
            {
                await _userService.BlockUserAsync(blockerUserId, userIdToBlock, request?.Comment);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Log exception
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("unblock/{userIdToUnblock}")]
        public async Task<IActionResult> UnblockUser(int userIdToUnblock)
        {
            var blockerUserId = GetCurrentUserId();
            if (blockerUserId <= 0) return Unauthorized();

            try
            {
                await _userService.UnblockUserAsync(blockerUserId, userIdToUnblock);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Log exception
                return BadRequest(ex.Message);
            }
        }
    }
}

