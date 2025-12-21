namespace Messenger.API.RequestDTOs
{
    public record SendChannelMessageRequest(
        int ChannelId,
        string MessageText,
        List<long>? FileAttachementIds = null,
        long? ReplyToMessageId = null);
}
