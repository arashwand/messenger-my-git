namespace Messenger.API.RequestDTOs
{
    // public record SendClassGroupMessageRequest(int ClassId, string MessageText, long? ReplyToMessageId = null);
    public record EditClassGroupMessageRequest(
        long MessageId,
        string MessageText,
        int GroupId,
        string GroupType,
        List<long>? FileAttachementIds = null,
        List<long>? FileIdsToRemove = null
    );
}
