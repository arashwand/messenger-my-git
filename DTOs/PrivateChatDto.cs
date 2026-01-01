namespace Messenger.DTOs
{
    public class PrivateChatDto
    {
        public long ConversationId { get; set; }
        public IEnumerable<MessageDto> Messages { get; set; }
        public bool IsSystemChat { get; set; }
    }
}
