using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;

namespace EnterpriseChat.Infrastructure.Messaging;

public sealed class MessageReadEventHandler
    : IDomainEventHandler<MessageReadEvent>
{
    private readonly IMessageBroadcaster _broadcaster;

    public MessageReadEventHandler(
        IMessageBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public async Task Handle(
        MessageReadEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _broadcaster.MessageReadAsync(
            domainEvent.MessageId,
            domainEvent.UserId);
    }
}
