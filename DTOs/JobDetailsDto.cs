namespace Messenger.DTOs
{
    /// <summary>
    /// DTO برای اطلاعات وضعیت Job در Hangfire
    /// </summary>
    public class JobDetailsDto
    {
        /// <summary>
        /// شناسه Job
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// وضعیت Job (Enqueued, Processing, Succeeded, Failed, etc.)
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// زمان ایجاد Job
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// پیغام خطا در صورت وجود
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
