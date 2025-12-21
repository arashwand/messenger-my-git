using Messenger.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IClassGroupService
    {
        Task<RequestUpsertClassGroupDto> CreateClassGroupAsync(RequestUpsertClassGroupDto model); 
        Task<RequestUpsertClassGroupDto> UpsertClassGroupAsync(RequestUpsertClassGroupDto model);
        Task<ClassGroupDto?> GetClassGroupByIdAsync(long userId, long targetId);
        Task<IEnumerable<ClassGroupDto>> GetUserClassGroupsAsync(long userId); 
        Task UpdateClassGroupInfoAsync(RequestUpsertClassGroupDto model); 
        Task DeleteClassGroupAsync(long targetId);

        // Class Group Members (UserClassGroup)
        Task AddUserToClassGroupAsync(long targetId, long userIdToAdd, long addedByUserId); 
        Task RemoveUserFromClassGroupAsync(long targetId, long userIdToRemove, long removedByUserId); 
        Task<IEnumerable<UserDto>> GetClassGroupMembersAsync(long userId, long targetId);

        /// <summary>
        /// برای استفاده داخلی سرویس‌ها
        /// </summary>
        /// <param name="targetId"></param>
        /// <returns></returns>
        Task<IEnumerable<UserDto>> GetClassGroupMembersInternalAsync( long targetId);

        /// <summary>
        /// دریافت تعداد اعضای گروه کلاسی
        /// </summary>
        /// <param name="targetId">شناسه گروه</param>
        /// <returns>تعداد اعضا</returns>
        Task<int> GetClassGroupMembersCountAsync(long targetId);
        
        Task<bool> IsUserMemberOfClassGroupAsync(long userId, long targetId);

        // Teacher specific
        Task<IEnumerable<RequestUpsertClassGroupDto>> GetTaughtClassGroupsAsync(long teacherUserId);
        Task<IEnumerable<ClassGroupDto>> GetAllClassGroupsAsync();

        // Upsert from portal model (includes members)
        Task UpsertClassGroupFromModelAsync(ClassGroupModel model);
    }
}

