namespace Messenger.DTOs
{
    // Define DTOs here or in a separate DTOs folder within WebApp if they become numerous/complex
    // These should match the parameters expected by IChatService methods
    // And also what chathub.js will send in the body of AJAX requests

    public class EditMessageRequestDto
    {
        public long UserId { get; set; }
        public long MessageId { get; set; }
        public string GroupId { get; set; }
        public string MessageText { get; set; }
        public string GroupType { get; set; }
        public long? ReplyToMessageId { get; set; }
        public List<long>? FileAttachementIds { get; set; }
        public List<long>? FileIdsToRemove { get; set; }
        public string ClientMessageId { get; set; }
    }
}
