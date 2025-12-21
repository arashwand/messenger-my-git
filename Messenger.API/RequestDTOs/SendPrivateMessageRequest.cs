namespace Messenger.API.RequestDTOs
{
    public record SendPrivateMessageRequest(int ReceiverUserId, string MessageText, List<long>? FileAttachementIds = null, long? ReplyToMessageId = null);

}
