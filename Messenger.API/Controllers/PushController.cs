using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Messenger.API.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    [Route("api/push")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly IManagePushService _pushService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pushService"></param>
        public PushController(IManagePushService pushService)
        {
            _pushService = pushService;
        }

        /// <summary>
        /// اشتراک کاربر
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            try
            {
                await _pushService.SubscribeAsync(userId, dto);
                return Ok();
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                return BadRequest("Failed to subscribe to push notifications.");
            }
        }

        /// <summary>
        /// لغو اشتراک کاربر
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("unsubscribe")]
        [Authorize]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            try
            {
                await _pushService.UnsubscribeAsync(userId, dto);
                return Ok();
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                return BadRequest("Failed to unsubscribe from push notifications.");
            }
        }
    }
}
