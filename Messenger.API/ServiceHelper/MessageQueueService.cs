using Hangfire;
using Hangfire.States;
using Messenger.DTOs;
using Microsoft.Extensions.Logging;

namespace Messenger.API.ServiceHelper
{
    /// <summary>
    /// اینترفیس سرویس مدیریت صف پیامها
    /// </summary>
    public interface IMessageQueueService
    {
        /// <summary>
        /// اضافه کردن پیام به صف با اولویت عادی
        /// </summary>
        /// <param name="message">اطلاعات پیام</param>
        /// <returns>شناسه Job</returns>
        string EnqueueMessage(QueuedMessageDto message);

        /// <summary>
        /// اضافه کردن پیام به صف با تاخیر
        /// </summary>
        /// <param name="message">اطلاعات پیام</param>
        /// <param name="delay">مدت زمان تاخیر</param>
        /// <returns>شناسه Job</returns>
        string EnqueueMessageWithDelay(QueuedMessageDto message, TimeSpan delay);

        /// <summary>
        /// اضافه کردن پیام به صف با اولویت مشخص
        /// </summary>
        /// <param name="message">اطلاعات پیام</param>
        /// <param name="priority">اولویت</param>
        /// <returns>شناسه Job</returns>
        string EnqueueMessageWithPriority(QueuedMessageDto message, MessagePriority priority);

        /// <summary>
        /// حذف Job از صف
        /// </summary>
        /// <param name="jobId">شناسه Job</param>
        /// <returns>true در صورت موفقیت</returns>
        bool DeleteJob(string jobId);

        /// <summary>
        /// دریافت وضعیت Job
        /// </summary>
        /// <param name="jobId">شناسه Job</param>
        /// <returns>اطلاعات وضعیت Job</returns>
        JobDetailsDto GetJobStatus(string jobId);
    }

    /// <summary>
    /// سرویس مدیریت صف پیامها با استفاده از Hangfire
    /// </summary>
    public class MessageQueueService : IMessageQueueService
    {
        private readonly ILogger<MessageQueueService> _logger;

        public MessageQueueService(ILogger<MessageQueueService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// اضافه کردن پیام به صف با اولویت عادی
        /// </summary>
        public string EnqueueMessage(QueuedMessageDto message)
        {
            try
            {
                _logger.LogInformation("Enqueuing message from user {UserId} to group {GroupId} ({GroupType})",
                    message.UserId, message.GroupId, message.GroupType);

                // تعیین صف بر اساس اولویت
                string queueName = GetQueueName(message.Priority);

                // استفاده از Queue attribute برای تعیین صف
                var client = new BackgroundJobClient();
                var jobId = client.Create<ProcessMessageJob>(
                    job => job.ProcessAsync(message, null),
                    new EnqueuedState(queueName));

                _logger.LogInformation("Message enqueued successfully with JobId: {JobId} in queue: {QueueName}", 
                    jobId, queueName);

                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing message from user {UserId}", message.UserId);
                throw;
            }
        }

        /// <summary>
        /// اضافه کردن پیام به صف با تاخیر
        /// </summary>
        public string EnqueueMessageWithDelay(QueuedMessageDto message, TimeSpan delay)
        {
            try
            {
                _logger.LogInformation("Scheduling message from user {UserId} to group {GroupId} with delay {Delay}",
                    message.UserId, message.GroupId, delay);

                var jobId = BackgroundJob.Schedule<ProcessMessageJob>(
                    job => job.ProcessAsync(message, null),
                    delay);

                _logger.LogInformation("Message scheduled successfully with JobId: {JobId}", jobId);

                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling message from user {UserId}", message.UserId);
                throw;
            }
        }

        /// <summary>
        /// اضافه کردن پیام به صف با اولویت مشخص
        /// </summary>
        public string EnqueueMessageWithPriority(QueuedMessageDto message, MessagePriority priority)
        {
            message.Priority = priority;
            return EnqueueMessage(message);
        }

        /// <summary>
        /// حذف Job از صف
        /// </summary>
        public bool DeleteJob(string jobId)
        {
            try
            {
                _logger.LogInformation("Deleting job {JobId}", jobId);
                return BackgroundJob.Delete(jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting job {JobId}", jobId);
                return false;
            }
        }

        /// <summary>
        /// دریافت وضعیت Job
        /// </summary>
        public JobDetailsDto GetJobStatus(string jobId)
        {
            try
            {
                var connection = Hangfire.JobStorage.Current.GetConnection();
                var jobData = connection.GetJobData(jobId);

                if (jobData == null)
                {
                    return new JobDetailsDto
                    {
                        JobId = jobId,
                        State = "NotFound"
                    };
                }

                return new JobDetailsDto
                {
                    JobId = jobId,
                    State = jobData.State,
                    CreatedAt = jobData.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
                return new JobDetailsDto
                {
                    JobId = jobId,
                    State = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// تعیین نام صف بر اساس اولویت
        /// </summary>
        private string GetQueueName(MessagePriority priority)
        {
            return priority switch
            {
                MessagePriority.Critical => "critical",
                MessagePriority.High => "high",
                MessagePriority.Low => "low",
                _ => "default"
            };
        }
    }
}
