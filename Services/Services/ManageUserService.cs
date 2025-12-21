using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace Messenger.Services.Services
{
    public class ManageUserService : IManageUserService
    {
        private readonly IEMessengerDbContext _context;
        private readonly ILogger<ManageUserService> _logger;

        public ManageUserService(IEMessengerDbContext context, ILogger<ManageUserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            _logger.LogInformation("Fetching all users.");
            var users = await _context.Users.ToListAsync();

            return users.Select(u => new UserDto
            {
                UserId = u.UserId,
                NameFamily = u.NameFamily,
                RoleName = u.RoleName,
                RoleFaName = u.RoleFaName,
                ProfilePicName = u.ProfilePicName,
                DeptName = u.DeptName
            }).ToList();
        }

        public async Task<UserDto?> GetUserByIdAsync(long userId)
        {
            _logger.LogInformation("Fetching user with ID {UserId}", userId);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            return new UserDto
            {
                UserId = user.UserId,
                NameFamily = user.NameFamily,
                RoleName = user.RoleName,
                RoleFaName = user.RoleFaName,
                ProfilePicName = user.ProfilePicName,
                DeptName = user.DeptName
            };
        }

        public async Task CreateUserAsync(UserDto user)
        {
            try
            {
                _logger.LogInformation("Creating user with ID {UserId}", user.UserId);

                if (await _context.Users.AnyAsync(f => f.UserId == user.UserId))
                {
                    _logger.LogWarning("User with ID {UserId} already exists", user.UserId);
                    throw new InvalidOperationException("کاربر تکراری است.");
                }

                var newUser = new User
                {
                    UserId = user.UserId,
                    NameFamily = user.NameFamily,
                    RoleName = user.RoleName,
                    RoleFaName = user.RoleFaName,
                    ProfilePicName = user.ProfilePicName,
                    DeptName = user.DeptName
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error in creating user with ID {UserId}", user.UserId);
                throw new InvalidOperationException(ex.Message);
                throw;
            }
        }

        public async Task UpdateUserAsync(UserDto user)
        {
            var existing = await _context.Users.FindAsync(user.UserId);
            if (existing == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for update", user.UserId);
                throw new InvalidOperationException("کاربر یافت نشد.");
            }

            existing.RoleName = user.RoleName;
            existing.RoleFaName = user.RoleFaName;
            existing.NameFamily = user.NameFamily;
            existing.DeptName = user.DeptName;
            existing.ProfilePicName = user.ProfilePicName;

            _context.Users.Update(existing);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(long userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for deletion", userId);
                throw new InvalidOperationException("کاربر یافت نشد.");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

}

