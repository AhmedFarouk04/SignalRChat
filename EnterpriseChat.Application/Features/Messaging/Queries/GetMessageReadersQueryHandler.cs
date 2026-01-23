using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

public sealed class GetMessageReadersQueryHandler
    : IRequestHandler<GetMessageReadersQuery, IReadOnlyList<MessageReadReceiptDto>>
{
    private readonly IMessageReceiptReadRepository _repository;
    private readonly IMessageRepository _messages;
    private readonly IRoomAuthorizationService _auth;

    public GetMessageReadersQueryHandler(
        IMessageReceiptReadRepository repository,
        IMessageRepository messages,
        IRoomAuthorizationService auth)
    {
        _repository = repository;
        _messages = messages;
        _auth = auth;
    }

    public async Task<IReadOnlyList<MessageReadReceiptDto>> Handle(GetMessageReadersQuery q, CancellationToken ct)
    {
        var msg = await _messages.GetByIdAsync(q.MessageId.Value, ct);
        if (msg is null) return Array.Empty<MessageReadReceiptDto>();

        await _auth.EnsureUserIsMemberAsync(msg.RoomId, q.UserId, ct);

        return await _repository.GetReadersAsync(q.MessageId, ct);
    }
}
