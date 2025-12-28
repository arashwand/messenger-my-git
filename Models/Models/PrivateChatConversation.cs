using System;
using System.ComponentModel.DataAnnotations;

namespace Messenger.Models.Models
{
    public class PrivateChatConversation
    {
        [Key]
        public long Id { get; set; }

        public long ConversationId { get; set; }

        public long User1Id { get; set; }

        public long User2Id { get; set; }

        public virtual User User1 { get; set; }

        public virtual User User2 { get; set; }
    }
}
