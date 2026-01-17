using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class DeliverRoomMessagesCommandHandler
    : IRequestHandler<DeliverRoomMessagesCommand, Unit>
{
    private readonly IMessageReceiptRepository _receiptRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IUnitOfWork _uow;

    public DeliverRoomMessagesCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IMessageRepository messageRepo,
        IUnitOfWork uow)
    {
        _receiptRepo = receiptRepo;
        _messageRepo = messageRepo;
        _uow = uow;
    }

    public async Task<Unit> Handle(
        DeliverRoomMessagesCommand request,
        CancellationToken ct)
    {
        var messages = await _messageRepo
            .GetByRoomAsync(request.RoomId, 0, 100, ct);

        foreach (var msg in messages)
        {
            var receipt = await _receiptRepo.GetAsync(msg.Id, request.UserId, ct);

            if (receipt is null)
                msg.MarkDelivered(request.UserId);
        }

        await _uow.CommitAsync(ct);

        return Unit.Value; 
    }
}
