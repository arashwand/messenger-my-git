using Messenger.DTOs;

namespace Messenger.DTOs
{
    public class PrivateChatDto
    {
        public Guid ConversationId { get; set; }
        public IEnumerable<MessageDto> Messages { get; set; }
    }
}