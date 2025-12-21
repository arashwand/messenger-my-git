using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]
using System.Security.Claims; // Added for ClaimsPrincipal

namespace MessengerApp.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Policy = "AdminPolicy")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
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


        [HttpGet("{userId}")]
        public async Task<ActionResult<UserDto>> GetUserById(long userId)
        {
            var user = await _userService.GetUserByIdAsync(userId, User);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<UserDto>>> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty.");
            }
            var users = await _userService.SearchUsersAsync(query);
            return Ok(users);
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

