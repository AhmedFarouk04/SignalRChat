using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;

public sealed class DeliverMessageCommandHandler
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IRoomAuthorizationService _auth;
    private readonly IUnitOfWork _uow;

    public DeliverMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,
        IRoomAuthorizationService auth,
        IUnitOfWork uow)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;
        _auth = auth;
        _uow = uow;
    }

    public async Task DeliverRoomMessagesAsync(
        RoomId roomId,
        UserId userId,
        CancellationToken ct = default)
    {
        await _auth.EnsureUserIsMemberAsync(roomId, userId, ct);

        var messages = await _messageRepo
            .GetByRoomAsync(roomId, 0, 100, ct);

        foreach (var msg in messages)
        {
            var receipt = await _receiptRepo.GetAsync(msg.Id, userId, ct);
            if (receipt is null)
                msg.MarkDelivered(userId);
        }

        await _uow.CommitAsync(ct);
    }
}
