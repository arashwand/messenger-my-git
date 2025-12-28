using Messenger.DTOs;

namespace Messenger.WebApp.ServiceHelper.Interfaces
{
    public interface IClassGroupServiceClient
    {
        Task<ClassGroupDto> CreateClassGroupAsync(ClassGroupDto model);
        Task<ClassGroupDto?> GetClassGroupByIdAsync(long classId);
        Task<IEnumerable<ClassGroupDto>> GetUserClassGroupsAsync(long userId);
        Task<IEnumerable<ClassGroupDto>> ShowUserClassGroupsAsync(long userId);
        Task<IEnumerable<ClassGroupDto>> GetAllClassGroupsAsync();
        Task<IEnumerable<ClassGroupDto>> GetTaughtClassGroupsAsync(long teacherUserId);
        Task<IEnumerable<ClassGroupDto>> GetTeacherTaughtClassGroupsAsync(long teacherUserId);
        Task UpdateClassGroupInfoAsync(ClassGroupDto model);
        Task DeleteClassGroupAsync(long classId);
        Task<IEnumerable<UserDto>> GetClassGroupMembersAsync(long classId);
        Task AddUserToClassGroupAsync(long classId, long userIdToAdd, long addedByUserId);
        Task RemoveUserFromClassGroupAsync(long classId, long userIdToRemove, long removedByUserId);
        Task<bool> IsUserMemberOfClassGroupAsync(long userId, long channelId);
    }
}
