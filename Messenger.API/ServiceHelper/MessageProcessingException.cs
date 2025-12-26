namespace Messenger.API.ServiceHelper
{
    /// <summary>
    /// استثنا برای خطاهای پردازش پیام در صف
    /// </summary>
    public class MessageProcessingException : Exception
    {
        public long? UserId { get; }
        public int? GroupId { get; }
        public string? GroupType { get; }

        public MessageProcessingException(string message) : base(message)
        {
        }

        public MessageProcessingException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        public MessageProcessingException(string message, long userId, string groupId, string groupType)
            : base(message)
        {
            UserId = userId;
            GroupId = groupId;
            GroupType = groupType;
        }

        public MessageProcessingException(string message, Exception innerException, long userId, string groupId, string groupType)
            : base(message, innerException)
        {
            UserId = userId;
            GroupId = groupId;
            GroupType = groupType;
        }
    }
}
