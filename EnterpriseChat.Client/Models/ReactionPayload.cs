using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.Client.Models;

public sealed class ReactionPayload
{
    public Guid MessageId { get; set; }
    public ReactionType Type { get; set; }
}
