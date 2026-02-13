using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class ReadMessageCommandHandler
    : IRequestHandler<ReadMessageCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IUnitOfWork _uow;
    private readonly IMessageRepository _messages;
    private readonly IRoomAuthorizationService _auth;
    private readonly IMessageBroadcaster _broadcaster;
    private readonly IChatRoomRepository _roomRepo;

    public ReadMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messages,
        IRoomAuthorizationService auth,
        IUnitOfWork uow,
        IMessageBroadcaster broadcaster,
        IChatRoomRepository roomRepo)
    {
        _receiptRepo = receiptRepo;
        _messages = messages;
        _auth = auth;
        _uow = uow;
        _broadcaster = broadcaster;
        _roomRepo = roomRepo;
    }

    public async Task<Unit> Handle(ReadMessageCommand command, CancellationToken ct)
    {
        var msg = await _messages.GetByIdAsync(command.MessageId.Value, ct);
        if (msg is null) return Unit.Value;

        await _auth.EnsureUserIsMemberAsync(msg.RoomId, command.UserId, ct);

        var receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);
        if (receipt is null) return Unit.Value;

        receipt.MarkRead();
        await _uow.CommitAsync(ct);

        var receipts = await _receiptRepo.GetReceiptsForMessageAsync(command.MessageId, ct);

        var notifyUsers = receipts
            .Select(r => r.UserId)
            .Append(msg.SenderId)
            .DistinctBy(u => u.Value)
            .ToList();

        await _broadcaster.MessageReadAsync(command.MessageId, msg.SenderId);

        await _broadcaster.MessageReadToAllAsync(command.MessageId, msg.SenderId, notifyUsers);

        await _broadcaster.MessageStatusUpdatedAsync(
            command.MessageId,
            command.UserId,
            MessageStatus.Read,
            notifyUsers);

        return Unit.Value;
    }

}