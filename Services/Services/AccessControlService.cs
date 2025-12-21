using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Messenger.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Messenger.Services.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس کنترل دسترسی
    /// </summary>
    public class AccessControlService : IAccessControlService
    {
        private readonly IEMessengerDbContext _context;
        private readonly IUserService _userService;
        private readonly ILogger<AccessControlService> _logger;

        public AccessControlService(
            IEMessengerDbContext context,
            IUserService userService,
            ILogger<AccessControlService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> CanAccessClassGroupAsync(long userId, long classGroupId)
        {
            _logger.LogInformation("بررسی دسترسی کاربر {UserId} به گروه کلاسی {ClassGroupId}", userId, classGroupId);

            // نقش مدیر یا پرسنل به همه گروه‌ها دسترسی دارند
            if (await IsAdminOrPersonelAsync(userId))
            {
                _logger.LogDebug("کاربر {UserId} نقش مدیریتی دارد - دسترسی مجاز", userId);
                return true;
            }

            // بررسی عضویت در گروه
            var isMember = await _context.UserClassGroups
                .AnyAsync(ucg => ucg.UserId == userId && ucg.ClassId == classGroupId);

            _logger.LogInformation("نتیجه بررسی عضویت کاربر {UserId} در گروه {ClassGroupId}: {IsMember}", userId, classGroupId, isMember);
            return isMember;
        }

        /// <inheritdoc/>
        public async Task<bool> CanEditClassGroupAsync(long userId, long classGroupId)
        {
            _logger.LogInformation("بررسی دسترسی ویرایش کاربر {UserId} برای گروه کلاسی {ClassGroupId}", userId, classGroupId);

            // نقش مدیر یا پرسنل می‌توانند ویرایش کنند
            if (await IsAdminOrPersonelAsync(userId))
            {
                _logger.LogDebug("کاربر {UserId} نقش مدیریتی دارد - دسترسی ویرایش مجاز", userId);
                return true;
            }

            // معلم گروه می‌تواند ویرایش کند
            return await IsTeacherOfClassGroupAsync(userId, classGroupId);
        }

        /// <inheritdoc/>
        public async Task<bool> CanManageClassGroupMembersAsync(long userId, long classGroupId)
        {
            _logger.LogInformation("بررسی دسترسی مدیریت اعضا توسط کاربر {UserId} برای گروه {ClassGroupId}", userId, classGroupId);

            // فقط نقش مدیر یا پرسنل می‌توانند اعضا را مدیریت کنند
            return await IsAdminOrPersonelAsync(userId);
        }

        /// <inheritdoc/>
        public async Task<bool> IsAdminOrPersonelAsync(long userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("کاربر با شناسه {UserId} یافت نشد", userId);
                return false;
            }

            var isAdmin = user.RoleName == ConstRoles.Manager || user.RoleName == ConstRoles.Personel;
            _logger.LogDebug("بررسی نقش مدیریتی کاربر {UserId}: {IsAdmin}", userId, isAdmin);
            return isAdmin;
        }

        /// <inheritdoc/>
        public async Task<bool> IsTeacherOfClassGroupAsync(long userId, long classGroupId)
        {
            var classGroup = await _context.ClassGroups.FindAsync(classGroupId);
            if (classGroup == null)
            {
                _logger.LogWarning("گروه کلاسی با شناسه {ClassGroupId} یافت نشد", classGroupId);
                return false;
            }

            var isTeacher = classGroup.TeacherUserId == userId;
            _logger.LogDebug("بررسی معلم بودن کاربر {UserId} برای گروه {ClassGroupId}: {IsTeacher}", userId, classGroupId, isTeacher);
            return isTeacher;
        }
    }
}