using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;

public sealed class MessageDeliveredEventHandler
    : IDomainEventHandler<MessageDeliveredEvent>
{
    private readonly IMessageBroadcaster _broadcaster;

    public MessageDeliveredEventHandler(
        IMessageBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public async Task Handle(
        MessageDeliveredEvent domainEvent,
        CancellationToken ct)
    {
        await _broadcaster.MessageDeliveredAsync(
            domainEvent.MessageId,
            domainEvent.UserId);
    }
}
