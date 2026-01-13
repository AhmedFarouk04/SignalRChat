using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Events;

public sealed class MessageDeliveredEvent : DomainEvent
{
    public MessageId MessageId { get; }
    public UserId UserId { get; }

    public MessageDeliveredEvent(
        MessageId messageId,
        UserId userId)
    {
        MessageId = messageId;
        UserId = userId;
    }
}
