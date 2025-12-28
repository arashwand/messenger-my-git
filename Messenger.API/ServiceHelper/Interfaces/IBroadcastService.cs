using Messenger.DTOs;

namespace Messenger.API.ServiceHelper.Interfaces
{
    public interface IBroadcastService
    {
        Task<BroadcastResultDto> BroadcastAsync(long senderUserId, SendMessageToAllFromPortalDto request);


        /// <summary>
        /// ارسال پیام به یک هدف خاص: فرد، گروه یا کانال
        /// </summary>
        /// <param name="senderUserId">ایدی ارسال کننده پیام</param>
        /// <param name="request">درخواست ارسال پیام شامل نوع هدف، ایدی هدف و متن پیام</param>
        /// <returns>نتیجه عملیات ارسال پیام</returns>
        Task<BroadcastResultDto> SendMessageAsync(long senderUserId, SendMessageToTargetDto request);


        /// <summary>
        /// ارسال نوتیفیکیشن به گروه در مورد پین یا آنپین شدن یک پیام
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="messageId"></param>
        /// <param name="isPinned"></param>
        /// <returns></returns>
        Task NotifyGroupAboutPinAsync(long userId, long messageId, bool isPinned);
    }
}
