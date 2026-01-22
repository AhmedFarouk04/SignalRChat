using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MarkRoomReadCommandHandler
    : IRequestHandler<MarkRoomReadCommand, Unit>
{
    private readonly IMessageRepository _messageRepo;
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IUnitOfWork _uow;
    private readonly IRoomAuthorizationService _auth;

    public MarkRoomReadCommandHandler(
        IMessageRepository messageRepo,
        IMessageReceiptRepository receiptRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _messageRepo = messageRepo;
        _receiptRepo = receiptRepo;
        _auth = auth;
        _uow = uow;
    }

    public async Task<Unit> Handle(MarkRoomReadCommand command, CancellationToken ct)
    {
        await _auth.EnsureUserIsMemberAsync(command.RoomId, command.UserId, ct);

        var messages = await _messageRepo.GetByRoomAsync(command.RoomId, 0, 200, ct);

        var lastMessage = messages.FirstOrDefault(m => m.Id == command.LastMessageId);
        if (lastMessage is null)
            return Unit.Value;

        var cutoffTime = lastMessage.CreatedAt;

        foreach (var msg in messages.Where(m => m.CreatedAt <= cutoffTime))
        {
            var receipt = await _receiptRepo.GetAsync(msg.Id, command.UserId, ct);
            if (receipt is null)
                continue;

            if (receipt.Status == MessageStatus.Read)
                continue;

            receipt.MarkRead();
        }

        await _uow.CommitAsync(ct);
        return Unit.Value;
    }
}
