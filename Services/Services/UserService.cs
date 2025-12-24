using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Messenger.Services.Interfaces.External;
using Messenger.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;


namespace Messenger.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IEMessengerDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IUserExternalApi _userExternalApi;

        public UserService(IEMessengerDbContext context, ILogger<UserService> logger, IUserExternalApi userExternalApi)
        {
            _context = context;
            _logger = logger;
            _userExternalApi = userExternalApi;
            _logger.LogInformation("UserService initialized.");
        }

        public async Task<UserDto?> GetUserByIdAsync(long userId, ClaimsPrincipal userClaims = null, bool useInternalService = false)
        {
            try
            {
                _logger.LogInformation("Getting user by ID: {UserId}", userId);
                var userEntity = await _context.Users.FindAsync(userId);
                if (userEntity == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found.", userId);
                    if (useInternalService)
                    {
                        var createNewUserInfoToApiDbBySso = await CreateUserBySsoInfo(userId);
                        return createNewUserInfoToApiDbBySso;
                    }
                    if (userClaims != null)
                    {
                        var createNewUserInfoToApiDb = await CreateUser(userId, userClaims);
                        if (createNewUserInfoToApiDb == null)
                            return null;

                        return new UserDto
                        {
                            UserId = createNewUserInfoToApiDb.UserId,
                            NameFamily = createNewUserInfoToApiDb.NameFamily,
                            RoleName = createNewUserInfoToApiDb.RoleName,
                            RoleFaName = createNewUserInfoToApiDb.RoleFaName,
                            ProfilePicName = createNewUserInfoToApiDb.ProfilePicName,
                            DeptName = createNewUserInfoToApiDb.DeptName
                        };

                    }
                    return null;
                }

                return new UserDto
                {
                    UserId = userEntity.UserId,
                    NameFamily = userEntity.NameFamily,
                    RoleName = userEntity.RoleName,
                    RoleFaName = userEntity.RoleFaName,
                    ProfilePicName = userEntity.ProfilePicName,
                    DeptName = userEntity.DeptName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Getting user by ID: {userId} has error. error message is: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> UserExistsAndHasRoleAsync(long userId, string roleName)
        {
            try
            {
                _logger.LogInformation("Getting user by ID: {UserId}", userId);
                var userEntity = await _context.Users.FindAsync(userId);
                if (userEntity == null)
                    return false;

                if (userEntity.RoleName == roleName)
                    return true;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Getting user by ID: {userId} has error. error message is: {ex.Message}");
                throw;
            }
        }

        private async Task<User?> CreateUser(long userId, ClaimsPrincipal userClaims)
        {

            try
            {
                var userInfo = GetUserInfoFromToken(userClaims);
                if (userInfo == null)
                {
                    return null;
                }

                //فقط کاربری که لاگین شده اطلاعاتش ثبت میشه و نمیتونه برای دیگران ثبت انجام بده
                // جهت جلوگیری از سو استفاده احتمالی
                if (userInfo.UserId != userId)
                {
                    return null;
                }

                var userDto = new User
                {
                    UserId = userId,
                    NameFamily = userInfo.NameFamily,
                    RoleName = userInfo.RoleName,
                    DeptName = userInfo.RoleName.ToLower(),
                    ProfilePicName = userInfo.ProfilePicName,
                    RoleFaName = userInfo.RoleFaName
                };

                try
                {
                    _context.Add(userDto);
                    await _context.SaveChangesAsync();
                    return userDto;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while creating user with ID {UserId}", userId);
                    return null;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error creating user with ID {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// ایجاد کاربر اگر در sso  وجود داشته باشد
        /// ایدی را میگیرد و اطلاعات را از sso  دریافت میکنه و به دیتابیس مسنجر اضافه میکنه
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userClaims"></param>
        /// <returns></returns>
        private async Task<UserDto?> CreateUserBySsoInfo(long userId)
        {

            try
            {
                var userInfo = _userExternalApi.GetUserByIdAsync(userId).Result;
                if (userInfo == null)
                {
                    return null;
                }

                if (userInfo.UserId <= 0)
                {
                    return null;
                }

                var user = new User();
                user.UserId = userId;
                user.NameFamily = userInfo?.NameFamily ?? "no name";
                user.RoleName = userInfo?.RoleName ?? ConstRoles.Student;
                user.DeptName = userInfo.RoleName != null ? userInfo.RoleName.ToLower() : ConstRoles.Student;
                user.ProfilePicName = userInfo?.ProfileImageName;
                user.RoleFaName = RoleFaName.GetRoleName(userInfo?.RoleName ?? ConstRoles.Student);


                try
                {
                    _context.Add(user);
                    await _context.SaveChangesAsync();


                    var userDto = new UserDto
                    {
                        UserId = user.UserId,
                        NameFamily = user.NameFamily,
                        RoleName = user.RoleName,
                        RoleFaName = user.RoleFaName,
                        ProfilePicName = user.ProfilePicName,
                        DeptName = user.DeptName
                    };
                    return userDto;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while creating user with ID {UserId}", userId);
                    return null;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error creating user with ID {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// کافیه اکسس توکن دریافتی رو بهش بدیم
        /// </summary>
        /// <returns></returns>
        private UserDto GetUserInfoFromToken(ClaimsPrincipal userClaims)
        {
            try
            {
                if (userClaims?.Claims == null)
                {
                    _logger.LogWarning("No claims provided in userClaims");
                    return null;
                }

                var roleName = userClaims.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                var nameFamily = userClaims.Claims.FirstOrDefault(c => c.Type == "NameFamily")?.Value;
                var profilePicName = userClaims.Claims.FirstOrDefault(c => c.Type == "ProfilePicName")?.Value ?? "UserIcon.png";
                var roleFaName = RoleFaName.GetRoleName(roleName);
                long userClaimUserId = 0;
                if (long.TryParse(userClaims.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out long result))
                {
                    userClaimUserId = result;
                }

                var userModel = new UserDto
                {
                    DeptName = roleName,
                    NameFamily = nameFamily,
                    ProfilePicName = "UserIcon.png",
                    RoleFaName = RoleFaName.GetRoleName(roleName),
                    RoleName = roleName,
                    UserId = userClaimUserId,

                };

                return userModel;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<IEnumerable<UserDto>> SearchUsersAsync(string query, string searchType = "name")
        {
            _logger.LogInformation("Searching users: query={Query}, type={SearchType}", query, searchType);
            
            IQueryable<User> usersQuery = _context.Users;

            // جستجو بر اساس نوع
            if (searchType == "nationalCode")
            {
                // TODO: در صورتی که فیلد کد ملی به مدل User اضافه شود، از کد زیر استفاده کنید:
                // usersQuery = usersQuery.Where(u => u.NationalCode.Contains(query));
                
                // فعلاً که فیلد کد ملی وجود ندارد، از جستجوی نام استفاده می‌کنیم
                _logger.LogWarning("National code search requested but field not available in User model. Falling back to name search.");
            }
            
            // جستجو بر اساس نام (برای هر دو حالت name و nationalCode تا زمانی که فیلد کد ملی اضافه شود)
            usersQuery = usersQuery.Where(u => 
                u.NameFamily.Contains(query) || 
                u.DeptName.Contains(query)
            );

            var userEntities = await usersQuery.ToListAsync();

            return userEntities.Select(s => new UserDto
            {
                UserId = s.UserId,
                NameFamily = s.NameFamily,
                RoleName = s.RoleName,
                RoleFaName = s.RoleFaName,
                ProfilePicName = s.ProfilePicName,
                DeptName = s.DeptName
            });
        }

        public async Task BlockUserAsync(long blockerUserId, long blockedUserId, string? comment = null)
        {
            _logger.LogInformation("User {BlockerUserId} attempting to block user {BlockedUserId}", blockerUserId, blockedUserId);
            var user = await _context.Users.Include(i => i.BlockedUsers).FirstOrDefaultAsync(f => f.UserId == blockedUserId);
            if (user == null)
            {
                _logger.LogError("User with ID {BlockedUserId} not found.", blockedUserId);
                throw new Exception("User not found");
            }

            var existingBlock = await IsBlockedAsync(blockedUserId);
            if (existingBlock)
            {
                _logger.LogWarning("User {BlockedUserId} is already blocked by {BlockerUserId}.", blockedUserId, blockerUserId);
                return;
            }

            var blockEntity = new BlockedUser
            {
                UserId = blockedUserId,
                BlockDate = DateTime.UtcNow,
                Comment = comment
            };
            _context.BlockedUsers.Add(blockEntity);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {BlockedUserId} successfully blocked by {BlockerUserId}.", blockedUserId, blockerUserId);
        }

        public async Task UnblockUserAsync(long creatorUserId, long blockedUserId)
        {
            _logger.LogInformation("User {CreatorUserId} attempting to unblock user {BlockedUserId}", creatorUserId, blockedUserId);
            var blockEntity = await _context.BlockedUsers
                .FirstOrDefaultAsync(b => b.UserId == blockedUserId);
            if (blockEntity != null)
            {
                _context.BlockedUsers.Remove(blockEntity);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {BlockedUserId} successfully unblocked by {CreatorUserId}.", blockedUserId, creatorUserId);
            }
            else
            {
                _logger.LogWarning("No block record found for user {BlockedUserId} by {CreatorUserId}.", blockedUserId, creatorUserId);
            }
        }

        public async Task<IEnumerable<BlockedUserDto>> GetBlockedUsersAsync(long userId)
        {
            _logger.LogInformation("Getting blocked users for user {UserId}", userId);
            var blockedEntities = await _context.BlockedUsers
                .Include(b => b.User)
                .Where(b => b.CreatorUserId == userId)
                .ToListAsync();

            return blockedEntities.Select(s => new BlockedUserDto
            {
                BlockDate = s.BlockDate,
                BlockedUserId = s.BlockedUserId,
                Comment = s.Comment,
                UserId = s.UserId,
            });
        }

        public async Task<bool> IsBlockedAsync(long userId)
        {
            _logger.LogInformation("Checking if user {UserId} is blocked.", userId);
            return await _context.BlockedUsers.AnyAsync(b => b.UserId == userId);
        }
    }
}

