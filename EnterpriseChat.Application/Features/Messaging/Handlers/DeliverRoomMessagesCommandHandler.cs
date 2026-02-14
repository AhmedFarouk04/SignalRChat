using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed class DeliverRoomMessagesCommandHandler : IRequestHandler<DeliverRoomMessagesCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;

    public DeliverRoomMessagesCommandHandler(
        IMessageRepository messageRepo,
        IMessageReceiptRepository receiptRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster)
    {
        _messageRepo = messageRepo;
        _receiptRepo = receiptRepo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
    }

    public async Task<Unit> Handle(DeliverRoomMessagesCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);

        Console.WriteLine($"[DeliverRoom] Starting for user {command.UserId.Value} in room {command.RoomId.Value}");

        // ✅ نجيب كل الرسائل اللي لسه متوصلتش للمستخدم ده
        var messages = await _messageRepo.GetUndeliveredForUserAsync(command.RoomId, command.UserId, ct);

        Console.WriteLine($"[DeliverRoom] Found {messages.Count} undelivered messages");

        if (!messages.Any())
            return Unit.Value;

        var deliveredSenders = new Dictionary<Guid, List<MessageId>>();
        var deliveredCount = 0;

        foreach (var msg in messages)
        {
            msg.MarkDelivered(command.UserId);
            deliveredCount++;

            if (!deliveredSenders.TryGetValue(msg.SenderId.Value, out var list))
            {
                list = new List<MessageId>();
                deliveredSenders[msg.SenderId.Value] = list;
            }
            list.Add(msg.Id);
        }

        await _uow.CommitAsync(ct);

        Console.WriteLine($"[DeliverRoom] Marked {deliveredCount} messages as DELIVERED");

        // ✅ البث للـ Senders
        if (_broadcaster is not null && deliveredSenders.Any())
        {
            var roomMembers = await _messageRepo.GetRoomMemberIdsAsync(command.RoomId, ct);

            foreach (var kv in deliveredSenders)
            {
                var senderId = new UserId(kv.Key);

                // لكل رسالة، نبث التحديث
                foreach (var msgId in kv.Value)
                {
                    // نجيب إحصائيات الرسالة بعد التحديث
                    var stats = await _receiptRepo.GetMessageStatsAsync(msgId, ct);

                    // نبث لكل أعضاء الغرفة
                    foreach (var memberId in roomMembers)
                    {
                        await _broadcaster.MessageStatusUpdatedAsync(
                            msgId,
                            command.UserId,
                            MessageStatus.Delivered,
                            new[] { memberId });

                        // لو العضو ده هو الـ sender، ابعتله الإحصائيات
                        if (memberId == senderId)
                        {
                            await _broadcaster.MessageReceiptStatsUpdatedAsync(
                                msgId.Value,
                                senderId.Value,
                                stats.TotalRecipients,
                                stats.DeliveredCount,
                                stats.ReadCount);
                        }
                    }
                }
            }
        }

        return Unit.Value;
    }
}