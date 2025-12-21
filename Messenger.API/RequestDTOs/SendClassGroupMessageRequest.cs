namespace Messenger.API.RequestDTOs
{
    // public record SendClassGroupMessageRequest(int ClassId, string MessageText, long? ReplyToMessageId = null);
    public record SendClassGroupMessageRequest(
     int ClassId,
     string MessageText,
     List<long>? FileAttachementIds = null,
     long? ReplyToMessageId = null
 );
}
