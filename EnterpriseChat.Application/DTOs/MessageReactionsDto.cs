using EnterpriseChat.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.DTOs
{
    public sealed class MessageReactionsDto
    {
        public Guid MessageId { get; set; }
        public Dictionary<ReactionType, int> Counts { get; set; } = new();
        public Dictionary<ReactionType, List<UserSummaryDto>> UsersByType { get; set; } = new();
        public Guid? CurrentUserReaction { get; set; }
        public ReactionType? CurrentUserReactionType { get; set; }
    }
}
