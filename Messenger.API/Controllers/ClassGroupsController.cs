using Messenger.API.RequestDTOs; 
using Messenger.DTOs;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Messenger.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClassGroupsController : ControllerBase
    {
        private readonly IClassGroupService _classGroupService;
        private readonly ILogger<ClassGroupsController> _logger;

        public ClassGroupsController(IClassGroupService classGroupService, ILogger<ClassGroupsController> logger)
        {
            _classGroupService = classGroupService;
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
        /// ایجاد گروه
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpPost]
        public async Task<ActionResult<RequestUpsertClassGroupDto>> CreateClassGroup([FromBody] RequestUpsertClassGroupDto request)
        {
            
            var currentUserId = GetCurrentUserId(); 
            if (currentUserId <= 0) return Unauthorized();

            if (request.ClassId <= 0)
            {
                return BadRequest("ClassId required!");
            }

            try
            {
                var classGroupDto = await _classGroupService.CreateClassGroupAsync(request);
                return Ok(classGroupDto);
            }
            catch (Exception ex)
            {
                // Log exception
                return BadRequest(ex.Message);
            }
        }

      
        /// <summary>
        /// گرفتن جزییات گروه توسط ایدی
        /// </summary>
        /// <param name="classId">ایدی گروه</param>
        /// <returns></returns>
        [HttpGet("{classId}")]
        public async Task<ActionResult<ClassGroupDto>> GetClassGroupById(long classId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
           
            var classGroup = await _classGroupService.GetClassGroupByIdAsync(userId, classId);
            if (classGroup == null) return NotFound();
            return Ok(classGroup);
        }



        /// <summary>
        /// تمام گروههای موجود را برای نقش ادمین بر میگردونه
        /// برای استفاده در پنل مدیریت
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<ClassGroupDto>>> GetAllClassGroups()
        {
            var classGroups = await _classGroupService.GetAllClassGroupsAsync();
            return Ok(classGroups);
        }


        /// <summary>
        /// گروههایی که کاربر در ان عضو است
        /// زمانی که کاربر لاگین شد، اتوماتیک درخواست ارسال میشه اینجا و لیست گروههایی که عضو هست برگردانده میشه
        /// </summary>
        /// <returns></returns>
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<ClassGroupDto>>> GetMyClassGroups()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var classGroups = await _classGroupService.GetUserClassGroupsAsync(userId);
            return Ok(classGroups);
        }


        /// <summary>
        /// ایدی کاربر رو میگیره و لیست گروههایی که کاربر در ان عضو هست رو میده
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpGet("userClassGroups")]
        public async Task<ActionResult<IEnumerable<ClassGroupDto>>> GetUserClassGroups(long userId)
        {
            //var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var classGroups = await _classGroupService.GetUserClassGroupsAsync(userId);
            return Ok(classGroups);
        }


        /// <summary>
        /// گروههایی که یک استاد  بعنوان مدرس تعین شده است
        /// </summary>
        /// <returns></returns>
        [HttpGet("taught")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<IEnumerable<RequestUpsertClassGroupDto>>> GetTaughtClassGroups()
        {
            // شاید خروجی باید IEnumerable<ClassGroupDto> باشد به جای IEnumerable<RequestUpsertClassGroupDto>
            var teacherUserId = GetCurrentUserId();
            if (teacherUserId <= 0) return Unauthorized();
            // Add permission check: Is the current user a teacher?

            var classGroups = await _classGroupService.GetTaughtClassGroupsAsync(teacherUserId);
            return Ok(classGroups);
        }


        /// <summary>
        /// لیست گروههایی که یک استاد تدریس میکنه را بر میگردونه
        /// استفاده برای ادمین
        /// </summary>
        /// <param name="teacherUserId"></param>
        /// <returns></returns>
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        [HttpGet("taught/{teacherUserId}")] // Endpoint for teachers to see groups they teach
        public async Task<ActionResult<IEnumerable<RequestUpsertClassGroupDto>>> GetTaughtClassGroups(long teacherUserId)
        {
            // شاید خروجی باید IEnumerable<ClassGroupDto> باشد به جای IEnumerable<RequestUpsertClassGroupDto
            // var teacherUserId = GetCurrentUserId();
            if (teacherUserId <= 0) return Unauthorized();
            // Add permission check: Is the current user a teacher?

            var classGroups = await _classGroupService.GetTaughtClassGroupsAsync(teacherUserId);
            return Ok(classGroups);
        }


        /// <summary>
        /// بروزرسانی گروه
        /// </summary>
        /// <param name="classId">ایدی گروه</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{classId}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> UpdateClassGroupInfo(long classId, [FromBody] RequestUpsertClassGroupDto request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user the teacher of this class group?

            try
            {
                await _classGroupService.UpdateClassGroupInfoAsync(request);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Handle specific exceptions like 'not found' or 'forbidden'
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// حذف یک گروه توسط ادمین
        /// </summary>
        /// <param name="classId">ایدی گروه</param>
        /// <returns></returns>
        [HttpDelete("{classId}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> DeleteClassGroup(long classId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user the teacher of this class group?

            try
            {
                await _classGroupService.DeleteClassGroupAsync(classId);
                return NoContent();
            }
            catch (Exception ex)
            {
                // Handle specific exceptions
                return BadRequest(ex.Message);
            }
        }

        // --- Class Group Members ---

        /// <summary>
        /// بدست اوردن اعضای یک گروه
        /// </summary>
        /// <param name="classId">ایدی گروه</param>
        /// <returns></returns>
        [HttpGet("{classId}/members")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetClassGroupMembers(long classId)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();
            // Add permission check: Is user a member or teacher of this class group?

            try
            {
                var members = await _classGroupService.GetClassGroupMembersAsync(userId, classId);
                return Ok(members);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// اضافه کردن یک فرد به گروه
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="userIdToAdd"></param>
        /// <returns></returns>
        [HttpPost("{classId}/members/{userIdToAdd}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> AddUserToClassGroup(long classId, long userIdToAdd)
        {
            var addedByUserId = GetCurrentUserId();
            if (addedByUserId <= 0) return Unauthorized();
            // Add permission check: Does addedByUserId have permission (e.g., is teacher)?

            try
            {
                await _classGroupService.AddUserToClassGroupAsync(classId, userIdToAdd, addedByUserId);
                return NoContent(); // Or Ok()
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// حذف یک کاربر از گروه
        /// </summary>
        /// <param name="classId"></param>
        /// <param name="userIdToRemove"></param>
        /// <returns></returns>
        [HttpDelete("{classId}/members/{userIdToRemove}")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> RemoveUserFromClassGroup(long classId, long userIdToRemove)
        {
            var removedByUserId = GetCurrentUserId();
            if (removedByUserId <= 0) return Unauthorized();
            // Add permission check: Does removedByUserId have permission (e.g., is teacher or self-removal)?

            try
            {
                await _classGroupService.RemoveUserFromClassGroupAsync(classId, userIdToRemove, removedByUserId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// بررسی اینکه یک کاربر عضو یک گروه خاص میباشد یانه
        /// </summary>
        /// <param name="classGroupId">ایدی گروه</param>
        /// <param name="userId">ایدی کاربر</param>
        /// <returns></returns>
        [HttpGet("{classGroupId}/members/{userId}/is-member")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<ActionResult<MemberDto>> IsUserMemberOfClassGroupAsync(long classGroupId, long userId)
        {
            var _userId = GetCurrentUserId();
            if (_userId <= 0) return Unauthorized();
            // Add permission check: Is user a member of this channel?

            try
            {
                bool isMember = await _classGroupService.IsUserMemberOfClassGroupAsync(userId, classGroupId);
                if (!isMember) return Forbid();

                _logger.LogInformation("User {UserId} membership status in class group {ClassId}: {IsMember}", userId, classGroupId, isMember);

                return Ok(new MemberDto
                {
                    UserId = userId,
                    ClassId = classGroupId,
                    IsMember = isMember
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking membership for user {UserId} in class group {ClassId}", userId, classGroupId);
                return StatusCode(500, new { error = "An unexpected error occurred while checking membership." });
            }
        }

        /// <summary>
        /// یک گروه کلاسی را بصورت کامل بروزرسانی میکنه 
        /// از استاد تا شاگرد و جزییات گروه
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("UpsertClassGroupFromPortal")]
        [Authorize(Roles = ConstRoles.Manager + "," + ConstRoles.Personel)]
        public async Task<IActionResult> UpsertClassGroupFromPortal([FromBody] ClassGroupModel model)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            try
            {
                await _classGroupService.UpsertClassGroupFromModelAsync(model);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting class group from portal");
                return BadRequest(ex.Message);
            }
        }
    }
}

