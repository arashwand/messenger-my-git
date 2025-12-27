using Messenger.DTOs;
using Messenger.WebApp.Hubs;
using Messenger.WebApp.ServiceHelper.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Channels;

namespace Messenger.WebApp.ServiceHelper
{
    public class RealtimeHubBridgeService : IRealtimeHubBridgeService
    {
        private readonly HubConnection _apiHubConnection;
        private readonly IHubContext<WebAppChatHub> _webAppHubContext;
        private readonly ILogger<RealtimeHubBridgeService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HubConnectionMonitor _connectionMonitor;

        public RealtimeHubBridgeService(IHubContext<WebAppChatHub> webAppHubContext,
                                        ILogger<RealtimeHubBridgeService> logger,
                                        IConfiguration configuration,
                                        HubConnectionMonitor connectionMonitor)
        {
            _webAppHubContext = webAppHubContext;
            _logger = logger;
            _configuration = configuration;
            _connectionMonitor = connectionMonitor;


            // اتصال به هاب اصلی در Web API
            var apiHubUrl = _configuration["SignalR:ApiHubUrl"];

            _apiHubConnection = new HubConnectionBuilder()
                .WithUrl(apiHubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connectionMonitor.StartMonitoring(_apiHubConnection, "API Hub");
            ConfigureApiHubListeners();
        }

        public bool IsConnected => _apiHubConnection.State == HubConnectionState.Connected;

        public async Task ConnectAsync()
        {
            try
            {
                await _apiHubConnection.StartAsync();
                _logger.LogInformation("Successfully connected to API Hub.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to API Hub.");
            }
        }

        public async Task DisconnectAsync()
        {
            await _apiHubConnection.StopAsync();
        }

        private void ConfigureApiHubListeners()
        {
            // شنود برای رویدادهایی که از API Hub می آیند و باید به کلاینت های WebApp ارسال شوند

            _apiHubConnection.On<MessageDto, string>("ReceiveMessage", (message, groupName) =>
            {
                _logger.LogInformation($"WebApp received message for group {groupName}. Forwarding to WebApp clients.");
                _webAppHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", message);
            });

            _apiHubConnection.On<MessageDto>("ReceiveEditedMessage", (message) =>
            {
                _webAppHubContext.Clients.All.SendAsync("ReceiveEditedMessage", message);
            });

            _apiHubConnection.On<string, int>("UpdateUnreadCount", (key, count) =>
            {
                // این رویداد را به همه کلاینت‌های متصل به WebAppChatHub ارسال کنید.
                _webAppHubContext.Clients.All.SendAsync("UpdateUnreadCount", key, count);
            });

            _apiHubConnection.On<long, bool, long, string>("UserStatusChanged", (userId, isOnline, groupId, groupType) =>
            {
                _webAppHubContext.Clients.All.SendAsync("UserStatusChanged", userId, isOnline, groupId, groupType);
            });


            _apiHubConnection.On<MessageDto, object>("MessageSentSuccessfully", (savedMessage, jsonObject) =>
            {
                _webAppHubContext.Clients.All.SendAsync("MessageSentSuccessfully", savedMessage, jsonObject);
            });


            _apiHubConnection.On<MessageDto, object>("EditMessageSentSuccessfully", (savedEditMessage, jsonObject) =>
            {
                _webAppHubContext.Clients.All.SendAsync("EditMessageSentSuccessfully", savedEditMessage, jsonObject);
            });

            _apiHubConnection.On<string>("MessageSentFailed", (clientMessageId) =>
            {
                _webAppHubContext.Clients.All.SendAsync("MessageSentFailed", clientMessageId);
            });

            _apiHubConnection.On<long>("EditMessageSentFailed", (messageId) =>
            {
                _webAppHubContext.Clients.All.SendAsync("EditMessageSentFailed", messageId);
            });

            _apiHubConnection.On<long, string, long>("UserTyping", (userId, fullName, groupId) =>
            {
                _webAppHubContext.Clients.All.SendAsync("UserTyping", userId, fullName, groupId);
            });

            _apiHubConnection.On<long, string, long>("UserStoppedTyping", (userId, fullName, groupId) =>
            {
                _webAppHubContext.Clients.All.SendAsync("UserStoppedTyping", userId, fullName, groupId);
            });


            _apiHubConnection.On<long, long, int, string>("MessageSeenUpdate", (messageId, readerUserId, seenCount, readerFullName) =>
            {
                _webAppHubContext.Clients.All.SendAsync("MessageSeenUpdate", messageId, readerUserId, seenCount, readerFullName);
            });


            _apiHubConnection.On<UpdatePinMessageDto>("UpdatePinMessage", (data) =>
            {
                _webAppHubContext.Clients.All.SendAsync("UpdatePinMessage", data);
            });

            _apiHubConnection.On<long, long, string, int>("MessageSuccessfullyMarkedAsRead", (messageId, groupId, groupType, unreadCount) =>
            {
                _webAppHubContext.Clients.All.SendAsync("MessageSuccessfullyMarkedAsRead", messageId, groupId, groupType, unreadCount);
            });


            _apiHubConnection.On<List<long>, long, string, int>("AllUnreadMessagesSuccessfullyMarkedAsRead", (messageIds, groupId, groupType, unreadCount) =>
            {
                _webAppHubContext.Clients.All.SendAsync("AllUnreadMessagesSuccessfullyMarkedAsRead", messageIds, groupId, groupType, unreadCount);
            });


            _apiHubConnection.On<long, bool>("UserDeleteMessage", (messageId, result) =>
            {
                _webAppHubContext.Clients.All.SendAsync("UserDeleteMessage", messageId, result);
            });

            //_apiHubConnection.On<long, bool>("UserSaveMessage", (messageId, result) =>
            //{
            //    _webAppHubContext.Clients.All.SendAsync("UserSaveMessage", messageId, result);
            //});
        }


        public async Task AnnounceUserDepartureAsync(long userId)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("AnnounceUserDeparture", userId);
        }

        public async Task SendTypingSignalAsync(long userId, long groupId, string groupType)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("SendTypingSignal", userId, groupId, groupType);
        }

        public async Task SendStopTypingSignalAsync(long userId, long groupId, string groupType)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("SendStopTypingSignal", userId, groupId, groupType);
        }

        public async Task MarkMessageAsReadAsync(long userId, long groupId, string groupType, long messageId)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("MarkMessageAsRead", userId, groupId, groupType, messageId);
        }

        public async Task MarkAllMessagesAsReadAsync(long userId, long groupId, string groupType)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("MarkAllMessagesAsRead", userId, groupId, groupType);
        }

        public async Task SendMessageAsync(SendMessageRequestDto request)
        {
            if (IsConnected)
            {
                await _apiHubConnection.InvokeAsync("SendMessage", request);
            }
            else
            {
                // اگر اتصال برقرار نبود، یک پیام خطا به کلاینت ارسال کن
                await _webAppHubContext.Clients.Client(request.ClientMessageId)
                                     .SendAsync("SendMessageError", "اتصال با سرور برقرار نیست. لطفاً دوباره تلاش کنید.");
            }
        }

        public async Task EditMessageAsync(EditMessageRequestDto request)
        {
            if (IsConnected)
            {
                await _apiHubConnection.InvokeAsync("EditMessage", request);
            }
            else
            {
                // اگر اتصال برقرار نبود، یک پیام خطا به کلاینت ارسال کن
                await _webAppHubContext.Clients.Client(request.MessageId.ToString())
                                     .SendAsync("SendMessageError", "اتصال با سرور برقرار نیست. لطفاً دوباره تلاش کنید.");
            }
        }

        public async Task SendHeartbeatAsync(long userId)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("SendHeartbeat", userId);
        }

        public async Task RequestUnreadCounts(long userId)
        {
            if (IsConnected)
                await _apiHubConnection.InvokeAsync("RequestUnreadCounts", userId);
        }

    }
}
