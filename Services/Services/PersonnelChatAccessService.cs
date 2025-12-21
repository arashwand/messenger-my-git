using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Services.Services;

public class PersonnelChatAccessService : IPersonnelChatAccessService
{
    private readonly IEMessengerDbContext _context;

    public PersonnelChatAccessService(IEMessengerDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="senderUserId"></param>
    /// <param name="chatId"></param>
    /// <param name="groupType"></param>
    /// <returns></returns>
    public async Task<bool> HasAccessToSendToChatAsync(long senderUserId, long chatId, string groupType)
    {
        var access = await _context.PersonelChatAccesses
            .FirstOrDefaultAsync(pca => pca.PersonelId == senderUserId &&
                                       pca.TargetId == chatId &&
                                       pca.GroupType == groupType &&
                                       pca.AccessSendMessageInChat);

        return access != null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="senderUserId"></param>
    /// <param name="chatId"></param>
    /// <param name="groupType"></param>
    /// <returns></returns>
    public async Task<bool> HasAccessToSendToIndividualsAsync(long senderUserId, long chatId, string groupType)
    {
        var access = await _context.PersonelChatAccesses
            .FirstOrDefaultAsync(pca => pca.PersonelId == senderUserId &&
                                       pca.TargetId == chatId &&
                                       pca.GroupType == groupType &&
                                       pca.AccessToStudentMessage);

        return access != null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="personelId"></param>
    /// <param name="targetId"></param>
    /// <param name="groupType"></param>
    /// <param name="accessSendMessageInChat"></param>
    /// <param name="accessToStudentMessage"></param>
    /// <returns></returns>
    public async Task UpsertAccessAsync(long personelId, int targetId, string groupType, bool accessSendMessageInChat, bool accessToStudentMessage)
    {
        var existingAccess = await _context.PersonelChatAccesses
            .FirstOrDefaultAsync(pca => pca.PersonelId == personelId &&
                                       pca.TargetId == targetId &&
                                       pca.GroupType == groupType);

        if (existingAccess != null)
        {
            // ?????????
            existingAccess.AccessSendMessageInChat = accessSendMessageInChat;
            existingAccess.AccessToStudentMessage = accessToStudentMessage;
            existingAccess.UpdatedAt = DateTime.UtcNow;
            _context.PersonelChatAccesses.Update(existingAccess);
        }
        else
        {
            // ????? ????
            var newAccess = new PersonelChatAccess
            {
                PersonelId = personelId,
                TargetId = targetId,
                GroupType = groupType,
                AccessSendMessageInChat = accessSendMessageInChat,
                AccessToStudentMessage = accessToStudentMessage,
                CreatedAt = DateTime.UtcNow
            };
            _context.PersonelChatAccesses.Add(newAccess);
        }

        await _context.SaveChangesAsync();
    }
}