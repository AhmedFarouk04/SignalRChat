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

        Console.WriteLine($"[Deliver] Starting for user {request.UserId.Value} in room {request.RoomId.Value}");

        var messages = await _messageRepo.GetByRoomForUpdateAsync(request.RoomId, 0, 200, ct);

        Console.WriteLine($"[Deliver] Found {messages.Count} messages to process");

        var deliveredSenders = new Dictionary<Guid, List<MessageId>>();

        int deliveredCount = 0;

        foreach (var msg in messages)
        {
            if (msg.SenderId == request.UserId) continue;

            var receipt = msg.Receipts.FirstOrDefault(r => r.UserId == request.UserId);
            if (receipt == null)
            {
                Console.WriteLine($"[Deliver] NO RECEIPT for msg {msg.Id} from user {request.UserId.Value}");
                continue;
            }

            if (receipt.Status >= MessageStatus.Delivered)
            {
                Console.WriteLine($"[Deliver] Already delivered: msg {msg.Id} status {receipt.Status}");
                continue;
            }

            msg.MarkDelivered(request.UserId);
            deliveredCount++;

            Console.WriteLine($"[Deliver] Marked delivered: msg {msg.Id} for user {request.UserId.Value}");

            if (!deliveredSenders.TryGetValue(msg.SenderId.Value, out var list))
            {
                list = new List<MessageId>();
                deliveredSenders[msg.SenderId.Value] = list;
            }
            list.Add(msg.Id);
        }

        Console.WriteLine($"[Deliver] Total newly delivered: {deliveredCount}");

        await _uow.CommitAsync(ct);

        if (_broadcaster is not null && deliveredSenders.Any())
        {
            Console.WriteLine($"[Deliver] Broadcasting {deliveredSenders.Sum(kv => kv.Value.Count)} deliveries");
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
