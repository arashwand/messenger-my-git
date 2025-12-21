using Messenger.API.ServiceHelper.Interfaces;
using Messenger.Models.Models;
using System.Text.Json;
using WebPush;

namespace Messenger.API.ServiceHelper
{
    public class PushService
    {
        private readonly IEMessengerDbContext _db;
        private readonly IRedisQueueClient _queueClient;
        private readonly string _vapidPublic;
        private readonly string _vapidPrivate;
        private readonly string _vapidSubject;
        private const string QueueName = "push_queue";

        public PushService(IEMessengerDbContext db,
            IRedisQueueClient queueClient,
            IConfiguration config)
        {
            _db = db;
            _queueClient = queueClient;

            _vapidPublic = config["Vapid:PublicKey"];
            _vapidPrivate = config["Vapid:PrivateKey"];
            _vapidSubject = config["Vapid:Subject"];
        }

        public async Task EnqueuePushAsync(string userId, string title, string body, string url)
        {
            // یافتن subscription های کاربر
            var subs = _db.PushSubscriptions
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Endpoint, s.P256dh, s.Auth })
                .ToList();

            foreach (var s in subs)
            {
                var payload = JsonSerializer.Serialize(new { title, body, url });
                var job = new PushJob
                {
                    Endpoint = s.Endpoint,
                    P256dh = s.P256dh,
                    Auth = s.Auth,
                    Payload = payload
                };
                var jobJson = JsonSerializer.Serialize(job);
                await _queueClient.EnqueueAsync(QueueName, jobJson);
            }
        }

        internal VapidDetails GetVapidDetails()
        {
            return new VapidDetails(_vapidSubject, _vapidPublic, _vapidPrivate);
        }

        /// <summary>
        /// مدل داخلی Job
        /// </summary>
        private class PushJob
        {
            public string Endpoint { get; set; }
            public string P256dh { get; set; }
            public string Auth { get; set; }
            public string Payload { get; set; }
        }
    }
}
