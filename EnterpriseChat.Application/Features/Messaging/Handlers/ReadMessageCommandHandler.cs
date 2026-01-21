using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;

public sealed class ReadMessageCommandHandler
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

    public async Task Handle(
        ReadMessageCommand command,
        CancellationToken ct = default)
    {
        var receipt = await _receiptRepo.GetAsync(
            command.MessageId,
            command.UserId,
            ct);

        if (receipt is null)
            return;

        receipt.MarkRead();
        await _uow.CommitAsync(ct);
    }
}
