using Messenger.DTOs;

namespace Messenger.WebApp.Models.ViewModels
{
    public class UserChatsViewModel
    {
        public List<ClassGroupDto> Groups { get; set; } = new();
        public List<ChannelDto> Channels { get; set; } = new();
        public List<PrivateChatItemDto> PrivateChats { get; set; } = new();
    }
}
