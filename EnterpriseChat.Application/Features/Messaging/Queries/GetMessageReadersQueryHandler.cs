using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries;

public sealed class GetMessageReadersQueryHandler
    : IRequestHandler<GetMessageReadersQuery, IReadOnlyList<MessageReadReceiptDto>>
{
    private readonly IMessageReceiptReadRepository _repository;

    public GetMessageReadersQueryHandler(IMessageReceiptReadRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<MessageReadReceiptDto>> Handle(GetMessageReadersQuery query, CancellationToken ct)
        => _repository.GetReadersAsync(query.MessageId, ct);
}
