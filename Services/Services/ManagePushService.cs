using Messenger.DTOs;
using Messenger.Models.Models;
using Messenger.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Messenger.Services.Services
{
    public class ManagePushService : IManagePushService
    {
        private readonly IEMessengerDbContext _db;

        public ManagePushService(IEMessengerDbContext db)
        {
            _db = db;
        }

        public async Task SubscribeAsync(string userId, PushSubscriptionDto dto)
        {
            var exists = await _db.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint && s.UserId == userId);

            if (exists == null)
            {
                var entity = new WebPushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.Keys.P256dh,
                    Auth = dto.Keys.Auth
                };
                _db.PushSubscriptions.Add(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task UnsubscribeAsync(string userId, UnsubscribeDto dto)
        {
            var sub = await _db.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint && s.UserId == userId);

            if (sub != null)
            {
                _db.PushSubscriptions.Remove(sub);
                await _db.SaveChangesAsync();
            }
        }
    }
}
