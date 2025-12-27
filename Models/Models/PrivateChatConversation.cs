using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Models.Models
{
    [Index(nameof(PublicId), IsUnique = true)]
    public class PrivateChatConversation
    {
        [Key]
        public long Id { get; set; }

        public Guid ConversationId { get; set; }

        public long PublicId { get; set; }

        public long User1Id { get; set; }

        public long User2Id { get; set; }

        public virtual User User1 { get; set; }

        public virtual User User2 { get; set; }
    }
}
