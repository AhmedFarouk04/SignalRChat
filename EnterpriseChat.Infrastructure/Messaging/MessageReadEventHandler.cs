using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Events;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Infrastructure.Messaging;

public sealed class MessageReadEventHandler : IDomainEventHandler<MessageReadEvent>
{
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IMessageRepository _messages;

    public MessageReadEventHandler(IMessageBroadcaster broadcaster, IMessageRepository messages)
    {
        _broadcaster = broadcaster;
        _messages = messages;
    }

    public async Task Handle(MessageReadEvent e, CancellationToken ct)
    {
        var msg = await _messages.GetByIdAsync(e.MessageId.Value, ct);
        if (msg is null) return;

        // ✅ ابعت للـ sender
        await _broadcaster.MessageReadAsync(e.MessageId, msg.SenderId);
    }
}
