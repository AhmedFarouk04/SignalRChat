using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;

public sealed record GetMessageReadersQuery(MessageId MessageId, UserId UserId)
    : IRequest<IReadOnlyList<MessageReadReceiptDto>>;
