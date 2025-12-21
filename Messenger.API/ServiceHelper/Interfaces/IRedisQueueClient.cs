namespace Messenger.API.ServiceHelper.Interfaces
{
    /// <summary>
    /// سرویس صف Redis برای مدیریت صف‌های پیام
    /// </summary>
    public interface IRedisQueueClient
    {
        /// <summary>
        /// نام صف و پیام برای افزودن به صف
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        Task EnqueueAsync(string queueName, string message);


        /// <summary>
        /// انصراف پیام از صف
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string?> DequeueAsync(string queueName, CancellationToken cancellationToken);
    }
}
