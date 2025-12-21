using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.API.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)] // پنهان کردن این کنترلر از مستندات API
    [Route("api/[controller]")]
    [Authorize(Roles = ConstRoles.Manager)] // برای محدود کردن دسترسی در محیط واقعی
    public class MaintenanceController : ControllerBase
    {
        private readonly IRedisUnreadManage _redisUnreadManage;
        private readonly ILogger<MaintenanceController> _logger;

        public MaintenanceController(IRedisUnreadManage redisUnreadManage, ILogger<MaintenanceController> logger)
        {
            _redisUnreadManage = redisUnreadManage;
            _logger = logger;
        }

        /// <summary>
        /// حذف تمام کلیدهای Redis مرتبط با مدیریت پیام‌های خوانده شده/نشده (برای تست).
        /// GET: api/Maintenance/ResetAllChatData
        /// </summary>
        [HttpGet("ResetAllChatData")]
        public async Task<IActionResult> ResetAllChatData()
        {
            try
            {
                var deletedCount = await _redisUnreadManage.ClearAllChatKeysAsync();

                _logger.LogInformation("Redis reset operation initiated by API. Total keys deleted: {DeletedCount}", deletedCount);

                return Ok(new
                {
                    Success = true,
                    Message = $"Successfully deleted {deletedCount} chat-related keys from Redis.",
                    DeletedCount = deletedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Redis reset operation.");
                return StatusCode(500, new { Success = false, Message = "An error occurred during Redis cleanup." });
            }
        }

        /// <summary>
        /// بازیابی تمام کلیدها و مقادیر Redis مرتبط با مدیریت پیام‌های خوانده شده/نشده برای یک کاربر خاص.
        /// GET: api/Maintenance/GetUserStatus?userId=123
        /// </summary>
        [HttpGet("GetUserStatus")]
        public async Task<IActionResult> GetUserStatus([FromQuery] long userId)
        {
            if (userId <= 0)
            {
                return BadRequest(new { Success = false, Message = "UserId is required and must be a positive number." });
            }

            try
            {
                var report = await _redisUnreadManage.GetUserStatusReportAsync(userId);

                _logger.LogInformation("Generated Redis status report for User {UserId}.", userId);

                return Ok(new
                {
                    Success = true,
                    UserId = userId,
                    Report = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Redis status report for user {UserId}.", userId);
                return StatusCode(500, new { Success = false, Message = "An error occurred during Redis status report generation." });
            }
        }
    }
}
