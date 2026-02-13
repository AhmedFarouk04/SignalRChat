using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeliverMessageCommandHandler
    : IRequestHandler<DeliverMessageCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IChatRoomRepository _roomRepo;

    public DeliverMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster,
        IChatRoomRepository roomRepo)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
        _roomRepo = roomRepo;
    }

    public async Task<Unit> Handle(DeliverMessageCommand command, CancellationToken ct)
    {
        var info = await _messageRepo.GetRoomAndSenderAsync(command.MessageId, ct);
        if (info is null) return Unit.Value;

        await _auth.EnsureUserIsMemberAsync(info.Value.RoomId, command.UserId, ct);

        var affected = await _receiptRepo.TryMarkDeliveredAsync(command.MessageId, command.UserId, ct);
        if (affected == 0) return Unit.Value;

        await _uow.CommitAsync(ct);

        // ✅ recipients الحقيقيين = اللي ليهم receipts على الرسالة
        var receipts = await _receiptRepo.GetReceiptsForMessageAsync(command.MessageId, ct);

        var notifyUsers = receipts
            .Select(r => r.UserId)
            .Append(info.Value.SenderId)
            .DistinctBy(u => u.Value)
            .ToList();

        // Sender (للـ legacy)
        await _broadcaster.MessageDeliveredAsync(command.MessageId, info.Value.SenderId);

        // ✅ ابعت بس للي يخصهم الأمر
        await _broadcaster.MessageDeliveredToAllAsync(command.MessageId, info.Value.SenderId, notifyUsers);

        await _broadcaster.MessageStatusUpdatedAsync(
            command.MessageId,
            command.UserId,
            MessageStatus.Delivered,
            notifyUsers);

        return Unit.Value;
    }

}