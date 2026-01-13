using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetMessageReadersQueryHandler
{
    private readonly IMessageReceiptReadRepository _repository;

    public GetMessageReadersQueryHandler(
        IMessageReceiptReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<MessageReadReceiptDto>> Handle(
        GetMessageReadersQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetReadersAsync(
            query.MessageId,
            cancellationToken);
    }
}
