using Messenger.DTOs;

namespace Messenger.WebApp.Models.ViewModels
{
    public class ChannelCreateViewModel
    {
        public string Name { get; set; }
        public string Title { get; set; }
    }

    public class ChannelEditViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
    }

    public class ChannelDetailsViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public long CreatorUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChannelMembersViewModel
    {
        public long ChannelId { get; set; }
        public string ChannelName { get; set; }
        public IEnumerable<UserDto> Members { get; set; }
    }

    public class AddMemberToChannelViewModel
    {
        public long ChannelId { get; set; }
        public long UserIdToAdd { get; set; }
    }

    public class RemoveMemberFromChannelViewModel
    {
        public long ChannelId { get; set; }
        public string ChannelName { get; set; }
        public long UserIdToRemove { get; set; }
    }
}
