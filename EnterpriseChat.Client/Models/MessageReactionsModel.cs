using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Client.Models
{
    public class MessageReactionsModel
    {
        public Guid MessageId { get; set; }
        public Dictionary<ReactionType, int> Counts { get; set; } = new();
        public Dictionary<ReactionType, List<UserModel>> UsersByType { get; set; } = new();
        public Guid? CurrentUserReaction { get; set; }
        public ReactionType? CurrentUserReactionType { get; set; }
    }
}
