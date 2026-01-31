using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeliverRoomMessagesCommandHandler
    : IRequestHandler<DeliverRoomMessagesCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster? _broadcaster;

    public DeliverRoomMessagesCommandHandler(
        IMessageRepository messageRepo,
        IRoomAuthorizationService authorizationService,
        IUnitOfWork uow,
        IMessageBroadcaster? broadcaster = null)
    {
        _messageRepo = messageRepo;
        _uow = uow;
        _auth = authorizationService;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(DeliverRoomMessagesCommand request, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(request.RoomId, request.UserId, ct);

        var messages = await _messageRepo.GetByRoomForUpdateAsync(request.RoomId, 0, 200, ct);

        var deliveredSenders = new Dictionary<Guid, List<MessageId>>(); // group by sender

        foreach (var msg in messages)
        {
            if (msg.SenderId == request.UserId) continue;

            msg.MarkDelivered(request.UserId);

            // جمع للbroadcast
            if (!deliveredSenders.TryGetValue(msg.SenderId.Value, out var list))
            {
                list = new List<MessageId>();
                deliveredSenders[msg.SenderId.Value] = list;
            }
            list.Add(msg.Id);
        }

        await _uow.CommitAsync(ct);

        // ✅ جديد: ابعت Delivered لكل sender
        if (_broadcaster is not null)
        {
            foreach (var kv in deliveredSenders)
            {
                var senderId = new UserId(kv.Key);
                foreach (var msgId in kv.Value)
                {
                    await _broadcaster.MessageDeliveredAsync(msgId, senderId);
                }
            }
        }

        return Unit.Value;
    }
}
