using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Entities;

public sealed class MessageDeletion
{
    public MessageId MessageId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime DeletedAt { get; private set; }

    private MessageDeletion() { }

    public MessageDeletion(MessageId messageId, UserId userId)
    {
        MessageId = messageId;
        UserId = userId;
        DeletedAt = DateTime.UtcNow;
    }
}