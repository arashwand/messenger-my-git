namespace Messenger.DTOs
{
    public class DeleteMessageRequestDto
    {
        public int GroupId { get; set; }
        public string GroupType { get; set; }
        public long MessageId { get; set; }

        public bool IsPortalMessage { get; set; } = false;
    }
}
