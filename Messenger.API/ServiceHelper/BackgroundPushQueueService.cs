using Messenger.API.ServiceHelper.Interfaces;
using Messenger.Models.Models;
using Microsoft.EntityFrameworkCore; 
using System.Net; 
using System.Text.Json;
using WebPush;

namespace Messenger.API.ServiceHelper
{
    public class BackgroundPushQueueService : BackgroundService
    {
        private readonly IRedisQueueClient _queueClient;
        private readonly IConfiguration _config;
        private readonly ILogger<BackgroundPushQueueService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string QueueName = "push_queue";

        public BackgroundPushQueueService(IRedisQueueClient queueClient,
            IConfiguration config,
            ILogger<BackgroundPushQueueService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _queueClient = queueClient;
            _config = config;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackgroundPushQueueService started.");

            var vapidPublic = _config["Vapid:PublicKey"];
            var vapidPrivate = _config["Vapid:PrivateKey"];
            var vapidSubject = _config["Vapid:Subject"];
            var vapid = new VapidDetails(vapidSubject, vapidPublic, vapidPrivate);
            var client = new WebPushClient();

            while (!stoppingToken.IsCancellationRequested)
            {
                PushJobInternal job = null;
                try
                {
                    var jobJson = await _queueClient.DequeueAsync(QueueName, stoppingToken);  // Changed from "QueueName" to QueueName

                    //var jobJson2 = await _queueClient.DequeueAsync("QueueName", stoppingToken);
                    if (jobJson == null) continue;

                    job = JsonSerializer.Deserialize<PushJobInternal>(jobJson);
                    if (job == null)
                    {
                        _logger.LogWarning("Cannot deserialize job: {JobJson}", jobJson);
                        continue;
                    }

                    var subscription = new WebPush.PushSubscription(job.Endpoint, job.P256dh, job.Auth);

                    // تلاش برای ارسال اعلان
                    await client.SendNotificationAsync(subscription, job.Payload, vapid);

                    _logger.LogInformation("Sent push to {Endpoint}", job.Endpoint);
                }
                catch (WebPushException wex)
                {
                    // مدیریت خطای WebPush
                    _logger.LogError(wex, "WebPushException: {Message}, Status: {Status}", wex.Message, wex.StatusCode);

                    // کد 410 (Gone) یا 404 (Not Found) یعنی اشتراک دیگر معتبر نیست
                    if (wex.StatusCode == HttpStatusCode.Gone || wex.StatusCode == HttpStatusCode.NotFound)
                    {
                        if (job != null && !string.IsNullOrEmpty(job.Endpoint))
                        {
                            await RemoveSubscriptionFromDb(job.Endpoint);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BackgroundPushQueueService loop");
                }
            }

            _logger.LogInformation("BackgroundPushQueueService stopping.");
        }

        // متد کمکی برای حذف از دیتابیس با ایجاد Scope جدید
        private async Task RemoveSubscriptionFromDb(string endpoint)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    // جایگزین کنید با DbContext یا Repository خودتان
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEMessengerDbContext>();

                    // فرض بر این است که جدولی به نام PushSubscriptions دارید
                    // که Endpoint در آن یکتا (Unique) است.
                    var subToRemove = await dbContext.PushSubscriptions
                                                     .FirstOrDefaultAsync(x => x.Endpoint == endpoint);

                    if (subToRemove != null)
                    {
                        dbContext.PushSubscriptions.Remove(subToRemove);
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Removed dead subscription for endpoint: {Endpoint}", endpoint);
                    }
                    else
                    {
                        _logger.LogWarning("Subscription not found in DB to delete: {Endpoint}", endpoint);
                    }
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to remove dead subscription from DB");
            }
        }

        /// <summary>
        /// مدل داخلی Job
        /// </summary>
        private class PushJobInternal
        {
            public string Endpoint { get; set; }
            public string P256dh { get; set; }
            public string Auth { get; set; }
            public string Payload { get; set; }
        }
    }
}
