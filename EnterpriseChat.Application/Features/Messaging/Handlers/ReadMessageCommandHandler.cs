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

    public ReadMessageCommandHandler(
        IMessageReceiptRepository receiptRepo,
        IUnitOfWork uow)
    {
        _receiptRepo = receiptRepo;
        _uow = uow;
    }

    public async Task<Unit> Handle(ReadMessageCommand command, CancellationToken ct)
    {
        var receipt = await _receiptRepo.GetAsync(command.MessageId, command.UserId, ct);
        if (receipt is null)
            return Unit.Value;

        receipt.MarkRead();
        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
