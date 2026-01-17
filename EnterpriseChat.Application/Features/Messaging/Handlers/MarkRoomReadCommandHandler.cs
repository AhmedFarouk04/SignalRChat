using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class MarkRoomReadCommandHandler
{
    private readonly IMessageRepository _messageRepo;
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IUnitOfWork _uow;

    public MarkRoomReadCommandHandler(
        IMessageRepository messageRepo,
        IMessageReceiptRepository receiptRepo,
        IUnitOfWork uow)
    {
        _messageRepo = messageRepo;
        _receiptRepo = receiptRepo;
        _uow = uow;
    }

    public async Task Handle(
        MarkRoomReadCommand command,
        CancellationToken ct = default)
    {
        // 1) Get messages in room
        var messages = await _messageRepo
            .GetByRoomAsync(command.RoomId, 0, 200, ct);

        // 2) Find last visible message
        var lastMessage = messages.FirstOrDefault(m =>
            m.Id == command.LastMessageId);

        if (lastMessage is null)
            return;

        var cutoffTime = lastMessage.CreatedAt;

        // 3) Mark all messages up to cutoff as Read
        foreach (var msg in messages.Where(m => m.CreatedAt <= cutoffTime))
        {
            var receipt = await _receiptRepo.GetAsync(
                msg.Id,
                command.UserId,
                ct);

            if (receipt is null)
                continue;

            if (receipt.Status == MessageStatus.Read)
                continue;

            receipt.MarkRead();
        }

        await _uow.CommitAsync(ct);
    }
}
