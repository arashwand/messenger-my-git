using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Messenger.DTOs;
using System.Threading.Tasks;

namespace Messenger.Services.Interfaces
{
    public interface IManagePushService
    {
        Task SubscribeAsync(string userId, PushSubscriptionDto dto);
        Task UnsubscribeAsync(string userId, UnsubscribeDto dto);
    }
}
