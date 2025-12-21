using System.Threading.Tasks;

namespace Messenger.Services.Interfaces;

public interface IPersonnelChatAccessService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="senderUserId"></param>
    /// <param name="chatId"></param>
    /// <param name="groupType"></param>
    /// <returns></returns>
    Task<bool> HasAccessToSendToChatAsync(long senderUserId, long chatId, string groupType);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="senderUserId"></param>
    /// <param name="chatId"></param>
    /// <param name="groupType"></param>
    /// <returns></returns>
    Task<bool> HasAccessToSendToIndividualsAsync(long senderUserId, long chatId, string groupType);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="personelId"></param>
    /// <param name="targetId"></param>
    /// <param name="groupType"></param>
    /// <param name="accessSendMessageInChat"></param>
    /// <param name="accessToStudentMessage"></param>
    /// <returns></returns>
    Task UpsertAccessAsync(long personelId, int targetId, string groupType, bool accessSendMessageInChat, bool accessToStudentMessage);
}