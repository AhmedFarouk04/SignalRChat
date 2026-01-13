using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeliverMessageCommandHandler
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;

    public DeliverMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,
        IUnitOfWork uow)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;
        _uow = uow;
    }

    // 🔥 NEW METHOD — REQUIRED BY ChatHub
    public async Task DeliverRoomMessagesAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        var messages = await _messageRepo
            .GetByRoomAsync(roomId, 0, 100, ct);

        foreach (var msg in messages)
        {
            var receipt = await _receiptRepo.GetAsync(msg.Id, userId, ct);

            if (receipt is null)
            {
                msg.MarkDelivered(userId);
            }
        }

        await _uow.CommitAsync(ct); 
    }
}
