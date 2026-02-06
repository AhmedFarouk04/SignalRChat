using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Client.Models
{
    public class ReactionModel
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public ReactionType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UserDisplayName { get; set; }
    }
}
