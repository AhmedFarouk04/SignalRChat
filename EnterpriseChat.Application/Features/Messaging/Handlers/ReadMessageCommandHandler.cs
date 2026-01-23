using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
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

    public ReadMessageCommandHandler(IMessageReceiptRepository receiptRepo, IMessageRepository messages, IRoomAuthorizationService auth, IUnitOfWork uow)
    {
        _receiptRepo = receiptRepo;
        _messages = messages;
        _auth = auth;
        _uow = uow;
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
        return Unit.Value;
    }

}
