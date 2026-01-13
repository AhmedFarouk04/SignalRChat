using EnterpriseChat.Domain.ValueObjects;
namespace EnterpriseChat.Domain.Events;
public sealed class MessageReadEvent : DomainEvent
{


    public MessageId MessageId { get; }
    public UserId UserId { get; }

    public MessageReadEvent(
        MessageId messageId,
        UserId userId)
    {
        MessageId = messageId;
        UserId = userId;
    }

}
