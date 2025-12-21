using Messenger.DTOs;
using Messenger.Services.Interfaces;
using MessengerApp.WebAPI.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminPolicy")] // Secure the entire controller
    public class ManageUsersController : Controller
    {
        private readonly IManageUserService _userService;
        private readonly ILogger<UsersController> _logger;
        private readonly IPersonnelChatAccessService _personnelChatAccessService;

        public ManageUsersController(IManageUserService userService, ILogger<UsersController> logger, IPersonnelChatAccessService personnelChatAccessService)
        {
            _userService = userService;
            _logger = logger;
            _personnelChatAccessService = personnelChatAccessService;
        }


        /// <summary>
        /// دریافت لیست همه کاربران
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }


        /// <summary>
        /// دریافت یک کاربر با ارسال ایدی ایشان
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "کاربر یافت نشد." });
            return Ok(user);
        }



        /// <summary>
        /// ایجاد کاربر
        /// </summary>
        /// <param name="userDto"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserDto userDto)
        {
            try
            {
                await _userService.CreateUserAsync(userDto);
                return Ok(new { message = "کاربر با موفقیت ایجاد شد." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        /// <summary>
        /// بروزرسانی کاربر
        /// </summary>
        /// <param name="userDto"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UserDto userDto)
        {
            try
            {
                await _userService.UpdateUserAsync(userDto);
                return Ok(new { message = "کاربر با موفقیت ویرایش شد." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }


        /// <summary>
        /// حذف کاربر
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _userService.DeleteUserAsync(id);
                return Ok(new { message = "کاربر با موفقیت حذف شد." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// مدیریت دسترسی پرسنل به چت (ایجاد یا بروزرسانی)
        /// </summary>
        /// <param name="request">اطلاعات دسترسی</param>
        /// <returns></returns>
        [HttpPost("manage-access")]
        public async Task<IActionResult> ManagePersonnelAccess([FromBody] PersonnelChatAccessRequest request)
        {
            try
            {
                await _personnelChatAccessService.UpsertAccessAsync(request.PersonelId, request.TargetId, request.GroupType, request.AccessSendMessageInChat, request.AccessToStudentMessage);
                return Ok(new { message = "دسترسی پرسنل با موفقیت مدیریت شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing personnel access for personelId {PersonelId}, targetId {TargetId}", request.PersonelId, request.TargetId);
                return BadRequest(new { message = "خطا در مدیریت دسترسی پرسنل." });
            }
        }
    }
}
