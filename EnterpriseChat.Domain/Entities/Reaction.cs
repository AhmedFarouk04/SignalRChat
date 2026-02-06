// EnterpriseChat.Domain/Entities/Reaction.cs
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class Reaction
{
    public ReactionId Id { get; private set; }
    public MessageId MessageId { get; private set; }
    public UserId UserId { get; private set; }
    public ReactionType Type { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Reaction() { }

    public Reaction(MessageId messageId, UserId userId, ReactionType type)
    {
        Id = ReactionId.New();
        MessageId = messageId;
        UserId = userId;
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateType(ReactionType newType)
    {
        Type = newType;
    }
}